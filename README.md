# Webhook Sender

A small desktop app for composing and previewing Discord webhook messages — plain messages, rich embeds, and interactive buttons.

It exists twice. I built the same tool from the same spec in two stacks to see how .NET/WPF and Python/Tkinter each solve the same problems — async I/O, keeping a UI responsive during a slow network call, and laying out a form.

---

## Features

Both versions do the same things:

- Compose plain messages, **embeds** (title, description, fields, colour, footer) and **buttons**
- **Live preview** of the rendered message before you send it
- **Discord rate-limit handling** — on a `429`, read `retry_after` from the response body, wait that long, then retry
- Webhook URL validated as a Discord webhook endpoint before anything is sent
- Per-message username override

---

## The two implementations

### C# / .NET 10 / WPF — `WebhookSender/`

```
WebhookSender/
  App.xaml           Application definition
  App.xaml.cs
  MainWindow.xaml    UI, including custom window chrome
  MainWindow.xaml.cs ~736 lines — payload construction, HTTP, retry, preview
  WebhookSender.csproj
```

- `HttpClient` with a 30-second timeout, `POST` with a `StringContent` body
- Rate-limit backoff: on failure the response is parsed with `JsonDocument` for `retry_after`, and the send is retried after that delay
- Custom-styled window with a live message preview panel
- Target framework `net10.0-windows`

**Run it:**

```bash
dotnet run --project WebhookSender
```

Builds to a self-contained Windows executable.

### Python / Tkinter — `webhook_sender.py`

A single file, ~596 lines.

- `requests` for transport
- **Background `threading`** so a slow or hanging endpoint never freezes the window — the send runs off the UI thread and the result is marshalled back
- `EmbedField`, `ActionButton` and `MessageWidget` classes model the three payload pieces
- Custom dark theme applied through a `THEME` dict and `hex_to_int` / `int_to_hex` helpers

**Run it:**

```bash
pip install requests
python webhook_sender.py
```

---

## Why two implementations?

Because the interesting part wasn't the webhook. It was the same four problems showing up in two very different runtimes:

| Problem | .NET / WPF | Python / Tkinter |
|---|---|---|
| Don't block the UI on network I/O | `async` / `await` on the dispatcher | `threading.Thread` + result marshalling |
| Structure the payload | anonymous objects → `JsonSerializer` | plain dicts → `json` |
| Build UI in code | XAML, designer-integrated | widget tree assembled in Python |
| Colour constants | strongly typed | hex strings converted to ints |

---

## Note on webhook URLs

A Discord webhook URL is effectively a **credential** — anyone holding it can post to that channel. Don't commit one, and don't paste one into an issue. This repo has no example URL committed for that reason; the field takes whatever you paste at runtime.

---

## Licence

MIT — see [LICENSE](LICENSE).

Written by [sablelight](https://github.com/sablelight).