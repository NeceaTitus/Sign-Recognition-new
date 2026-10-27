# Sign Recognition — agent AI din Slack

Această integrare rulează în cloud, fără calculatorul local:

`Slack /ai` → `Cloudflare Worker Free` → `GitHub Actions Free` → `Gemini API Free` → branch + Pull Request

## Ce este deja pregătit

- `worker/src/index.js`: endpoint Slack verificat criptografic și filtrare după user ID.
- `worker/wrangler.toml`: configurația Cloudflare Worker.
- `.github/workflows/ai-agent.yml`: workflow declanșabil din Slack.
- `scripts/ai_agent.py`: trimite contextul proiectului către Gemini și aplică doar patch-uri validate.

## Configurare

### 1. Adaugă fișierele în repo

Copiază fișierele din acest pachet în rădăcina repo-ului `Sign-Recognition-new` și fă commit pe `main`.

### 2. Creează secretul GitHub

În repo: **Settings → Secrets and variables → Actions → New repository secret**.

Adaugă:

- `GEMINI_API_KEY`: cheia gratuită din Google AI Studio.
- `AGENT_GITHUB_TOKEN`: token GitHub fine-grained cu acces doar la acest repo, permisiuni **Contents: Read and write** și **Pull requests: Read and write**.

Nu pune aceste valori în fișiere sau în Slack.

### 3. Creează Slack App

În Slack API, creează o aplicație din manifest sau manual. Adaugă un Slash Command:

- Command: `/ai`
- Request URL: URL-ul Worker-ului Cloudflare
- Short description: `Comandă agentul AI pentru repo`

Invită aplicația în canalul `#sign-recognition`. Notează `Signing Secret` din **Basic Information** și ID-urile Slack ale celor doi utilizatori.

### 4. Publică Worker-ul

Din directorul `worker`:

```bash
npm install -g wrangler
wrangler login
wrangler deploy
wrangler secret put SLACK_SIGNING_SECRET
wrangler secret put GITHUB_TOKEN
```

După deployment, copiază URL-ul Worker-ului în configurația Slash Command-ului Slack.

În `wrangler.toml`, înlocuiește `U_FIRST,U_SECOND` cu cele două user ID-uri Slack, apoi rulează din nou `wrangler deploy`.

### 5. Test

În `#sign-recognition`:

```text
/ai explică structura proiectului fără să modifici fișiere
```

Apoi testează o modificare mică. Agentul creează un branch `ai/slack-...` și un Pull Request; `main` nu este modificat direct.

## Protecții

- Sunt acceptați doar cei doi user ID-uri configurați.
- Semnăturile Slack și timestamp-urile sunt verificate.
- Agentul primește doar fișiere text relevante, fără `Library`, `Temp`, `UserSettings` sau build-uri.
- Patch-ul este verificat cu `git apply --check` și `git diff --check`.
- Workflow-ul are timeout de 15 minute.
- Nu există push direct în `main`.

## Limitări gratuite

GitHub Actions și Gemini Free au limite de utilizare. Dacă limita este depășită, jobul eșuează fără să fie taxat, atât timp cât nu este activată facturarea. Proiectul Unity nu este compilat automat în această primă versiune, deoarece build-urile Unity necesită configurarea licenței și consumă multe minute; workflow-ul verifică patch-ul și creează Pull Request-ul.
