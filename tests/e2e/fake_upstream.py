#!/usr/bin/env python3
"""Deterministic fake upstream for the .NET SDK end-to-end suite.

Serves, on one port:
  GET  *                      model lists (OpenAI /v1/models and Ollama /api/tags shapes)
  POST */chat/completions     OpenAI-compatible chat: plain, streaming (SSE), tool calls, skill verdicts
  POST /mcp                   MCP JSON-RPC 2.0 (tools/list, tools/call) for a `lookup` tool
  POST /mcp/call              the REST fallback the built-in runner uses
  GET  /_log                  every request seen, for assertions

Behaviour is keyed on the conversation so tests can steer it:
  - a skill review (response_format json_schema "skill_verdict"): "E2E_REJECT" in the prompt -> reject,
    "E2E_REVIEW" -> needs_review, otherwise accept
  - tools offered and the last user message contains "use the lookup tool": call `lookup` first
  - otherwise answer "fake: <last user message>", echoing any system prompt marker as "[system: ...]"

Usage: python3 fake_upstream.py PORT
"""

import json
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

LOG = []


def last_user_text(messages):
    for m in reversed(messages or []):
        if m.get("role") == "user":
            c = m.get("content")
            if isinstance(c, list):
                return " ".join(p.get("text", "") for p in c if isinstance(p, dict))
            return c or ""
    return ""


def system_text(messages):
    for m in messages or []:
        if m.get("role") == "system" and isinstance(m.get("content"), str):
            return m["content"]
    return ""


def has_tool_result(messages):
    return any(m.get("role") == "tool" for m in messages or [])


def usage():
    return {"prompt_tokens": 3, "completion_tokens": 4, "total_tokens": 7}


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):
        pass

    def _json(self, body, code=200):
        data = json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path.startswith("/_log"):
            self._json(LOG)
            return
        self._json({
            "object": "list",
            "data": [{"id": "llama3.2", "object": "model", "owned_by": "fake"}],
            "models": [{"name": "llama3.2", "model": "llama3.2"}],
        })

    def do_POST(self):
        length = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(length) if length else b""
        try:
            body = json.loads(raw or b"{}")
        except json.JSONDecodeError:
            body = {}
        LOG.append({"path": self.path, "auth": self.headers.get("Authorization"), "body": body})

        if self.path.rstrip("/").endswith("/mcp"):
            self._mcp(body)
        elif self.path.endswith("/mcp/call"):
            self._json({"content": [{"type": "text", "text": f"order {body.get('arguments', {}).get('order')}: shipped (rest)"}]})
        elif self.path.endswith("/chat/completions"):
            self._chat(body)
        else:
            self._json({"error": "unknown path " + self.path}, 404)

    def _mcp(self, req):
        method, rid = req.get("method"), req.get("id")
        if method == "tools/list":
            result = {"tools": [{"name": "lookup", "description": "Look up an order", "inputSchema": {
                "type": "object", "properties": {"order": {"type": "integer"}}}}]}
        elif method == "tools/call":
            args = (req.get("params") or {}).get("arguments") or {}
            result = {"content": [{"type": "text", "text": f"order {args.get('order')}: shipped"}]}
        elif method == "initialize":
            result = {"protocolVersion": "2025-03-26", "capabilities": {"tools": {}}, "serverInfo": {"name": "fake", "version": "1"}}
        else:
            self._json({"jsonrpc": "2.0", "id": rid, "error": {"code": -32601, "message": "no method " + str(method)}})
            return
        self._json({"jsonrpc": "2.0", "id": rid, "result": result})

    def _chat(self, req):
        messages = req.get("messages") or []
        fmt = req.get("response_format") or {}
        if (fmt.get("json_schema") or {}).get("name") == "skill_verdict":
            prompt = json.dumps(messages)
            verdict = "reject" if "E2E_REJECT" in prompt else "needs_review" if "E2E_REVIEW" in prompt else "accept"
            self._complete(req, json.dumps({"verdict": verdict, "reasoning": f"fake reviewer: {verdict}"}))
            return

        tools = req.get("tools") or []
        user = last_user_text(messages)
        if tools and "use the lookup tool" in user and not has_tool_result(messages):
            call = {"id": "call-1", "type": "function", "function": {"name": tools[0]["function"]["name"], "arguments": json.dumps({"order": 42})}}
            self._complete(req, None, tool_calls=[call])
            return

        if has_tool_result(messages):
            result = [m for m in messages if m.get("role") == "tool"][-1].get("content")
            text = f"tool said: {result}"
        else:
            text = f"fake: {user}"
        marker = system_text(messages)
        if marker.startswith("E2E:"):
            text += f" [system: {marker}]"
        self._complete(req, text)

    def _complete(self, req, text, tool_calls=None):
        if not req.get("stream"):
            message = {"role": "assistant", "content": text}
            if tool_calls:
                message["tool_calls"] = tool_calls
            self._json({"id": "c1", "object": "chat.completion", "created": 0, "model": req.get("model", "llama3.2"),
                        "choices": [{"index": 0, "message": message, "finish_reason": "tool_calls" if tool_calls else "stop"}],
                        "usage": usage()})
            return

        chunks = []
        if tool_calls:
            delta = {"role": "assistant", "tool_calls": [dict(tc, index=i) for i, tc in enumerate(tool_calls)]}
            chunks.append({"choices": [{"index": 0, "delta": delta, "finish_reason": None}]})
            chunks.append({"choices": [{"index": 0, "delta": {}, "finish_reason": "tool_calls"}]})
        else:
            for word in (text or "").split(" "):
                chunks.append({"choices": [{"index": 0, "delta": {"content": word + " "}, "finish_reason": None}]})
            chunks.append({"choices": [{"index": 0, "delta": {}, "finish_reason": "stop"}]})
        chunks.append({"choices": [], "usage": usage()})

        payload = "".join(f"data: {json.dumps(dict(c, id='c1', object='chat.completion.chunk', created=0, model='llama3.2'))}\n\n" for c in chunks)
        payload += "data: [DONE]\n\n"
        data = payload.encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 19100
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
