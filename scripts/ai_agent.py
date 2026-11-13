import json, os, pathlib, subprocess, sys, urllib.request

ROOT = pathlib.Path.cwd()
COMMAND = os.environ["AGENT_COMMAND"]
API_KEY = os.environ["GEMINI_API_KEY"]

allowed_ext = {".cs", ".asmdef", ".json", ".md", ".txt", ".shader", ".uxml", ".uss", ".asset", ".yaml", ".yml"}
ignored = {"Library", "Temp", "Logs", "Builds", "UserSettings", ".git", "Packages"}
files = []
for p in ROOT.rglob("*"):
    if not p.is_file() or any(part in ignored for part in p.parts):
        continue
    if p.suffix.lower() not in allowed_ext and p.name not in {"README.md", "manifest.json"}:
        continue
    try:
        text = p.read_text(errors="ignore")
    except Exception:
        continue
    if len(text) <= 30000:
        files.append(f"\n--- FILE: {p.relative_to(ROOT)} ---\n{text}")
context = "".join(files)
if len(context) > 180000:
    context = context[:180000]

prompt = f'''You are a careful senior Unity C# engineer modifying a public GitHub repository.
User request: {COMMAND}
Repository: NeceaTitus/Sign-Recognition-new, Unity 6000.2.4f1.
Return ONLY valid JSON with keys summary and patch. patch must be a standard unified diff that applies with `git apply`.
Rules: modify only files shown in the context; do not add binaries; do not change project secrets; do not edit .gitignore; keep changes minimal; add or update tests only when practical; if no safe change is possible, return an empty patch and explain in summary.

Repository context:
{context}
'''

body = {"contents":[{"parts":[{"text":prompt}]}], "generationConfig":{"temperature":0.15,"responseMimeType":"application/json"}}
req = urllib.request.Request(
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key=" + API_KEY,
    data=json.dumps(body).encode(), headers={"Content-Type":"application/json"})
with urllib.request.urlopen(req, timeout=120) as r:
    result = json.load(r)
text = result["candidates"][0]["content"]["parts"][0]["text"]
answer = json.loads(text)
patch = answer.get("patch", "")
summary = answer.get("summary", "No summary provided")
if patch:
    pathlib.Path("/tmp/agent.patch").write_text(patch)
    subprocess.run(["git", "apply", "--check", "/tmp/agent.patch"], check=True)
    subprocess.run(["git", "apply", "--whitespace=error", "/tmp/agent.patch"], check=True)
    subprocess.run(["git", "diff", "--check"], check=True)
    changed = "true"
else:
    changed = "false"
with open(os.environ["GITHUB_OUTPUT"], "a") as out:
    out.write(f"changed={changed}\n")
    safe = summary.replace("%", "%25").replace("\n", "%0A").replace("\r", "%0D")
    out.write(f"summary={safe}\n")
print(summary)
