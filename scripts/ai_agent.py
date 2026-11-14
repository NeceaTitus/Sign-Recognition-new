import json, os, pathlib, re, subprocess, urllib.request

ROOT = pathlib.Path.cwd()
COMMAND = os.environ["AGENT_COMMAND"]
API_KEY = os.environ["GEMINI_API_KEY"]
allowed_ext = {".cs", ".asmdef", ".json", ".md", ".txt", ".shader", ".uxml", ".uss", ".asset", ".yaml", ".yml"}
ignored = {"Library", "Temp", "Logs", "Builds", "UserSettings", ".git", "Packages"}
requested = {x.lower() for x in re.findall(r"[\w./\\-]+\.cs\b", COMMAND, re.I)}
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
    rel = str(p.relative_to(ROOT)).replace("\\", "/")
    if len(text) <= 30000 or p.name.lower() in requested or rel.lower() in requested:
        files.append((p.name.lower() in requested or rel.lower() in requested, rel, text))
files.sort(key=lambda x: (not x[0], x[1]))
context = "".join(f"\n--- FILE: {rel} ---\n{text}" for _, rel, text in files)
context = context[:180000]

prompt = f'''You are a careful senior Unity C# engineer.
User request: {COMMAND}
Repository: NeceaTitus/Sign-Recognition-new, Unity 6000.2.4f1.
Return ONLY valid JSON with exactly these keys: summary and edits.
summary must be a short string. edits must be an array of objects with exactly: path, old, new.
Each path must be an existing repository-relative path from the context.
For each edit, old must match an exact non-empty substring in that file, including whitespace; new is its replacement.
Do not use Markdown fences. Do not return unified diff. Do not add binaries, secrets, or files. Keep changes minimal.
If no safe change is possible, return edits as [] and explain in summary.

Repository context:
{context}
'''
body = {"contents": [{"parts": [{"text": prompt}]}], "generationConfig": {"temperature": 0.1, "responseMimeType": "application/json"}}
req = urllib.request.Request(
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key=" + API_KEY,
    data=json.dumps(body).encode(), headers={"Content-Type": "application/json"})
import time

result = None
last_error = None

for attempt in range(3):
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            result = json.load(r)
        break
    except Exception as error:
        last_error = error
        if attempt < 2:
            time.sleep(5)

if result is None:
    raise RuntimeError(
        f"Gemini nu a răspuns după 3 încercări: {last_error}"
    )

    result = json.load(r)
answer = json.loads(result["candidates"][0]["content"]["parts"][0]["text"])
summary = str(answer.get("summary", "No summary provided"))
edits = answer.get("edits", [])
changed = False
for edit in edits:
    rel = str(edit.get("path", "")).replace("\\", "/")
    candidates = {str(p.relative_to(ROOT)).replace("\\", "/"): p for p in ROOT.rglob("*") if p.is_file()}
    actual = candidates.get(rel) or {k.lower(): v for k, v in candidates.items()}.get(rel.lower())
    if not actual:
        raise ValueError(f"Unknown edit path: {rel}")
    old, new = str(edit.get("old", "")), str(edit.get("new", ""))
    if not old:
        raise ValueError(f"Empty old text for: {rel}")
    text = actual.read_text(errors="ignore")
    if text.count(old) != 1:
        raise ValueError(f"Expected exactly one match in {rel}, found {text.count(old)}")
    actual.write_text(text.replace(old, new, 1))
    changed = True
if changed:
    subprocess.run(["git", "diff", "--check"], check=True)
with open(os.environ["GITHUB_OUTPUT"], "a") as out:
    out.write(f"changed={'true' if changed else 'false'}\n")
    safe = summary.replace("%", "%25").replace("\n", "%0A").replace("\r", "%0D")
    out.write(f"summary={safe}\n")
print(summary)
