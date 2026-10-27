const encoder = new TextEncoder();

function hex(buffer) {
  return [...new Uint8Array(buffer)].map(b => b.toString(16).padStart(2, "0")).join("");
}

async function verifySlack(request, rawBody, secret) {
  const timestamp = request.headers.get("x-slack-request-timestamp") || "";
  const signature = request.headers.get("x-slack-signature") || "";
  const now = Math.floor(Date.now() / 1000);
  if (!timestamp || Math.abs(now - Number(timestamp)) > 300) return false;
  const key = await crypto.subtle.importKey("raw", encoder.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const digest = hex(await crypto.subtle.sign("HMAC", key, encoder.encode(`v0:${timestamp}:${rawBody}`)));
  const expected = `v0=${digest}`;
  if (signature.length !== expected.length) return false;
  let diff = 0;
  for (let i = 0; i < expected.length; i++) diff |= signature.charCodeAt(i) ^ expected.charCodeAt(i);
  return diff === 0;
}

function formToObject(rawBody) {
  return Object.fromEntries(new URLSearchParams(rawBody));
}

async function dispatchWorkflow(command, form, env) {
  const url = `https://api.github.com/repos/${env.GITHUB_OWNER}/${env.GITHUB_REPO}/actions/workflows/${env.WORKFLOW_FILE || "ai-agent.yml"}/dispatches`;
  const response = await fetch(url, {
    method: "POST",
    headers: {
      "Authorization": `Bearer ${env.GITHUB_TOKEN}`,
      "Accept": "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
      "User-Agent": "sign-recognition-slack-agent",
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      ref: "main",
      inputs: {
        command,
        slack_response_url: form.response_url || "",
        slack_user_id: form.user_id || "",
        slack_channel_id: form.channel_id || ""
      }
    })
  });
  if (!response.ok) throw new Error(`GitHub dispatch failed: ${response.status} ${await response.text()}`);
}

export default {
  async fetch(request, env, ctx) {
    if (request.method !== "POST") return new Response("Sign Recognition AI bridge", { status: 200 });
    const rawBody = await request.text();
    if (!(await verifySlack(request, rawBody, env.SLACK_SIGNING_SECRET))) return new Response("Invalid Slack signature", { status: 401 });
    const form = formToObject(rawBody);
    const allowed = (env.ALLOWED_SLACK_USER_IDS || "").split(",").map(x => x.trim()).filter(Boolean);
    if (!allowed.includes(form.user_id)) return new Response("Nu ai permisiune să folosești agentul.", { status: 403 });
    const command = (form.text || "").trim();
    if (!command) return new Response(JSON.stringify({ response_type: "ephemeral", text: "Scrie comanda după /ai." }), { headers: { "Content-Type": "application/json" } });
    ctx.waitUntil(dispatchWorkflow(command, form, env));
    return new Response(JSON.stringify({ response_type: "ephemeral", text: "Am primit comanda. Agentul lucrează și va trimite Pull Request-ul aici." }), { headers: { "Content-Type": "application/json" } });
  }
};
