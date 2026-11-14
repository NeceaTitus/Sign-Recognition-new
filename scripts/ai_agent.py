import json, os, pathlib, subprocess, sys, urllib.request

ROOT = pathlib.Path.cwd()
COMMAND = os.environ["AGENT_COMMAND"]
API_KEY = os.environ["GEMINI_API_KEY"]

allowed_ext = {".cs", ".asmdef", ".json", ".md", ".txt", ".shader", ".uxml", ".uss", ".asset", ".yaml", ".yml"}
ignored = {"Library", "Temp", "Logs", "Builds", "UserSettings", ".git", "Packages"}
import re

requested_names = {
    item.lower()
    for item in re.findall(r"[\w./\\-]+\.cs\b", COMMAND, re.IGNORECASE)
}

candidates = []

for p in ROOT.rglob("*"):
    if not p.is_file() or any(part in ignored for part in p.parts):
        continue

    if p.suffix.lower() not in allowed_ext and p.name not in {"README.md", "manifest.json"}:
        continue

    try:
        text = p.read_text(errors="ignore")
    except Exception:
        continue

    relative = str(p.relative_to(ROOT)).replace("\\", "/").lower()
    basename = p.name.lower()
    is_requested = basename in requested_names or relative in requested_names

    if len(text) <= 30000 or is_requested:
        candidates.append((is_requested, relative, text))

# Fișierele menționate în comandă intră primele în context.
candidates.sort(key=lambda item: (not item[0], item[1]))

files = []
total_chars = 0
max_context = 180000

for is_requested, relative, text in candidates:
    file_block = f"\n--- FILE: {relative} ---\n{text}"

    if total_chars + len(file_block) > max_context:
        if is_requested:
            # Fișierele cerute explicit au prioritate chiar dacă sunt mari.
            files.append(file_block)
        continue

    files.append(file_block)
    total_chars += len(file_block)

context = "".join(files)

prompt = f'''You are a careful senior Unity C# engineer modifying a public GitHub repository.
User request: {COMMAND}
Repository: NeceaTitus/Sign-Recognition-new, Unity 6000.2.4f1.
Return ONLY valid JSON with keys summary and patch.

The patch must be a valid standard unified diff accepted by:
git apply --check

Every modified file must have a complete diff header:
--- a/path/to/file
+++ b/path/to/file

Every hunk must have a valid hunk header like:
@@ -10,3 +10,4 @@

Do not return Markdown code fences.
Do not return explanations outside the JSON.
If you cannot create a valid patch, return an empty patch string.
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
patch = answer.get("patch", "").strip()

if patch.startswith("```"):
    patch_lines = patch.splitlines()

    if patch_lines and patch_lines[0].startswith("```"):
        patch_lines = patch_lines[1:]

    if patch_lines and patch_lines[-1].strip() == "```":
        patch_lines = patch_lines[:-1]

    patch = "\n".join(patch_lines).strip()

summary = answer.get("summary", "No summary provided")
if patch:
    actual_paths = {}

    for repo_file in ROOT.rglob("*"):
        if repo_file.is_file():
            relative = str(repo_file.relative_to(ROOT)).replace("\\", "/")
            actual_paths[relative.lower()] = relative

    normalized_lines = []

    for line in patch.splitlines():
        if line.startswith("--- ") or line.startswith("+++ "):
            marker, path = line[:4], line[4:]

            # Elimină prefixul temporar al patch-ului.
            prefix = ""
            clean_path = path

            if clean_path.startswith("a/") or clean_path.startswith("b/"):
                prefix = clean_path[:2]
                clean_path = clean_path[2:]

            normalized_path = actual_paths.get(clean_path.lower(), clean_path)
            line = marker + prefix + normalized_path

        normalized_lines.append(line)

    patch = "\n".join(normalized_lines).strip()

    pathlib.Path("/tmp/agent.patch").write_text(patch)
    subprocess.run(
        ["git", "apply", "--check", "/tmp/agent.patch"],
        check=True
    )
    subprocess.run(
        ["git", "apply", "--whitespace=error", "/tmp/agent.patch"],
        check=True
    )
    subprocess.run(["git", "diff", "--check"], check=True)
    changed = "true"
else:
    changed = "false"

with open(os.environ["GITHUB_OUTPUT"], "a") as out:
    out.write(f"changed={changed}\n")
    safe = summary.replace("%", "%25").replace("\n", "%0A").replace("\r", "%0D")
    out.write(f"summary={safe}\n")
print(summary)
