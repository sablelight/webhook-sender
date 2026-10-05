import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext, colorchooser
import threading
import json
import requests
from datetime import datetime
import re

THEME = {
    "bg": "#2b2d31",
    "fg": "#dbdee1",
    "input_bg": "#1e1f22",
    "input_fg": "#dbdee1",
    "accent": "#5865f2",
    "accent_hover": "#4752c4",
    "success": "#23a55a",
    "error": "#da373c",
    "border": "#3f4147",
    "card_bg": "#2b2d31",
    "section_bg": "#232428",
    "text_secondary": "#949ba4",
}


def hex_to_int(hex_color):
    hex_color = hex_color.lstrip("#")
    return int(hex_color, 16)


def int_to_hex(int_color):
    return f"#{int_color:06x}"


class EmbedField:
    def __init__(self, parent, container, remove_callback):
        self.parent = parent
        self.container = container
        self.remove_callback = remove_callback
        self.frame = tk.Frame(container, bg=THEME["card_bg"])
        self.frame.pack(fill="x", pady=(0, 6))

        self.name_var = tk.StringVar()
        self.value_var = tk.StringVar()
        self.inline_var = tk.BooleanVar(value=False)

        row = tk.Frame(self.frame, bg=THEME["card_bg"])
        row.pack(fill="x")

        tk.Entry(row, textvariable=self.name_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=18).pack(side="left", padx=(0, 4))
        tk.Entry(row, textvariable=self.value_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=30).pack(side="left", padx=(0, 4))
        tk.Checkbutton(row, text="Inline", variable=self.inline_var,
                       bg=THEME["card_bg"], fg=THEME["fg"], selectcolor=THEME["input_bg"],
                       activebackground=THEME["card_bg"], activeforeground=THEME["fg"]).pack(side="left")
        tk.Button(row, text="X", command=self.remove, bg=THEME["error"],
                  fg="white", relief="flat", font=("Segoe UI", 8, "bold"),
                  width=2, cursor="hand2").pack(side="left", padx=(4, 0))

    def remove(self):
        self.frame.destroy()
        self.remove_callback(self)

    def to_dict(self):
        return {"name": self.name_var.get(), "value": self.value_var.get(),
                "inline": self.inline_var.get()}


class ActionButton:
    def __init__(self, parent, container, remove_callback):
        self.parent = parent
        self.container = container
        self.remove_callback = remove_callback
        self.frame = tk.Frame(container, bg=THEME["card_bg"])
        self.frame.pack(fill="x", pady=(0, 6))

        self.label_var = tk.StringVar()
        self.url_var = tk.StringVar()

        row = tk.Frame(self.frame, bg=THEME["card_bg"])
        row.pack(fill="x")

        tk.Label(row, text="Label:", bg=THEME["card_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9)).pack(side="left", padx=(0, 4))
        tk.Entry(row, textvariable=self.label_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=15).pack(side="left", padx=(0, 8))
        tk.Label(row, text="URL:", bg=THEME["card_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9)).pack(side="left", padx=(0, 4))
        tk.Entry(row, textvariable=self.url_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=35).pack(side="left", padx=(0, 4))
        tk.Button(row, text="X", command=self.remove, bg=THEME["error"],
                  fg="white", relief="flat", font=("Segoe UI", 8, "bold"),
                  width=2, cursor="hand2").pack(side="left")

    def remove(self):
        self.frame.destroy()
        self.remove_callback(self)

    def to_dict(self):
        return {"label": self.label_var.get(), "url": self.url_var.get()}


class MessageWidget:
    def __init__(self, parent, notebook, msg_index, remove_callback):
        self.parent = parent
        self.notebook = notebook
        self.msg_index = msg_index
        self.remove_callback = remove_callback
        self.fields = []
        self.buttons = []

        self.tab = ttk.Frame(notebook)
        self.notebook.add(self.tab, text=f"Message {msg_index + 1}")

        canvas = tk.Canvas(self.tab, bg=THEME["bg"], highlightthickness=0)
        scrollbar = ttk.Scrollbar(self.tab, orient="vertical", command=canvas.yview)
        self.scrollable = tk.Frame(canvas, bg=THEME["bg"])

        self.scrollable.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.create_window((0, 0), window=self.scrollable, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)

        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")

        self._bind_mousewheel(canvas)
        self._build_ui()

    def _bind_mousewheel(self, canvas):
        def on_mousewheel(event):
            canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")

        def bind_all(event):
            canvas.bind_all("<MouseWheel>", on_mousewheel)

        def unbind_all(event):
            canvas.unbind_all("<MouseWheel>")

        canvas.bind("<Enter>", bind_all)
        canvas.bind("<Leave>", unbind_all)

    def _make_section(self, parent, title):
        frame = tk.Frame(parent, bg=THEME["section_bg"], highlightbackground=THEME["border"],
                         highlightthickness=1, padx=12, pady=10)
        frame.pack(fill="x", pady=(0, 10), padx=5)
        tk.Label(frame, text=title, bg=THEME["section_bg"], fg=THEME["accent"],
                 font=("Segoe UI", 11, "bold")).pack(anchor="w", pady=(0, 8))
        return frame

    def _build_ui(self):
        s = self.scrollable

        tk.Label(s, text="Message Content", bg=THEME["bg"], fg=THEME["text_secondary"],
                 font=("Segoe UI", 9)).pack(anchor="w", padx=5, pady=(0, 2))
        self.content_text = scrolledtext.ScrolledText(s, bg=THEME["input_bg"],
                                                       fg=THEME["input_fg"], insertbackground=THEME["fg"],
                                                       relief="flat", font=("Segoe UI", 10),
                                                       height=5, wrap="word")
        self.content_text.pack(fill="x", padx=5, pady=(0, 10))

        embed_frame = self._make_section(s, "Embed")
        self._build_embed(embed_frame)

        fields_frame = self._make_section(s, "Fields")
        self.fields_container = tk.Frame(fields_frame, bg=THEME["section_bg"])
        self.fields_container.pack(fill="x")
        tk.Button(fields_frame, text="+ Add Field", command=self.add_field,
                  bg=THEME["accent"], fg="white", relief="flat",
                  font=("Segoe UI", 9), cursor="hand2").pack(anchor="w", pady=(6, 0))

        buttons_frame = self._make_section(s, "Link Buttons")
        self.buttons_container = tk.Frame(buttons_frame, bg=THEME["section_bg"])
        self.buttons_container.pack(fill="x")
        tk.Button(buttons_frame, text="+ Add Button", command=self.add_button,
                  bg=THEME["accent"], fg="white", relief="flat",
                  font=("Segoe UI", 9), cursor="hand2").pack(anchor="w", pady=(6, 0))

    def _build_embed(self, parent):
        grid = tk.Frame(parent, bg=THEME["section_bg"])
        grid.pack(fill="x")

        self.title_var = tk.StringVar()
        self.desc_var = tk.StringVar()
        self.url_var = tk.StringVar()
        self.color_var = tk.StringVar(value="#5865F2")

        row1 = tk.Frame(grid, bg=THEME["section_bg"])
        row1.pack(fill="x", pady=(0, 6))
        tk.Label(row1, text="Title:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        tk.Entry(row1, textvariable=self.title_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10)).pack(side="left", fill="x", expand=True)

        row2 = tk.Frame(grid, bg=THEME["section_bg"])
        row2.pack(fill="x", pady=(0, 6))
        tk.Label(row2, text="URL:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        tk.Entry(row2, textvariable=self.url_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10)).pack(side="left", fill="x", expand=True)

        row3 = tk.Frame(grid, bg=THEME["section_bg"])
        row3.pack(fill="x", pady=(0, 6))
        tk.Label(row3, text="Color:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        self.color_btn = tk.Button(row3, textvariable=self.color_var,
                                    command=self._pick_color, bg=THEME["input_bg"],
                                    fg=THEME["fg"], relief="flat",
                                    font=("Segoe UI", 10), cursor="hand2")
        self.color_btn.pack(side="left")

        row4 = tk.Frame(grid, bg=THEME["section_bg"])
        row4.pack(fill="x", pady=(0, 6))
        tk.Label(row4, text="Description:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        tk.Entry(row4, textvariable=self.desc_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10)).pack(side="left", fill="x", expand=True)

        sep = tk.Frame(grid, bg=THEME["border"], height=1)
        sep.pack(fill="x", pady=6)

        row5 = tk.Frame(grid, bg=THEME["section_bg"])
        row5.pack(fill="x", pady=(0, 6))
        tk.Label(row5, text="Author:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        self.author_name = tk.StringVar()
        self.author_icon = tk.StringVar()
        self.author_url = tk.StringVar()
        tk.Entry(row5, textvariable=self.author_name, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=15).pack(side="left", padx=(0, 4))
        tk.Entry(row5, textvariable=self.author_icon, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=20).pack(side="left", padx=(0, 4))
        tk.Entry(row5, textvariable=self.author_url, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=20).pack(side="left")

        row6 = tk.Frame(grid, bg=THEME["section_bg"])
        row6.pack(fill="x", pady=(0, 6))
        tk.Label(row6, text="Thumbnail:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        self.thumb_var = tk.StringVar()
        tk.Entry(row6, textvariable=self.thumb_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10)).pack(side="left", fill="x", expand=True)

        row7 = tk.Frame(grid, bg=THEME["section_bg"])
        row7.pack(fill="x", pady=(0, 6))
        tk.Label(row7, text="Image:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        self.image_var = tk.StringVar()
        tk.Entry(row7, textvariable=self.image_var, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10)).pack(side="left", fill="x", expand=True)

        row8 = tk.Frame(grid, bg=THEME["section_bg"])
        row8.pack(fill="x", pady=(0, 6))
        tk.Label(row8, text="Footer:", bg=THEME["section_bg"], fg=THEME["fg"],
                 font=("Segoe UI", 9), width=8, anchor="w").pack(side="left")
        self.footer_text = tk.StringVar()
        self.footer_icon = tk.StringVar()
        tk.Entry(row8, textvariable=self.footer_text, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=20).pack(side="left", padx=(0, 4))
        tk.Entry(row8, textvariable=self.footer_icon, bg=THEME["input_bg"],
                 fg=THEME["input_fg"], insertbackground=THEME["fg"],
                 relief="flat", font=("Segoe UI", 10), width=25).pack(side="left")

        row9 = tk.Frame(grid, bg=THEME["section_bg"])
        row9.pack(fill="x")
        self.timestamp_var = tk.BooleanVar(value=False)
        tk.Checkbutton(row9, text="Add Timestamp", variable=self.timestamp_var,
                       bg=THEME["section_bg"], fg=THEME["fg"], selectcolor=THEME["input_bg"],
                       activebackground=THEME["section_bg"], activeforeground=THEME["fg"],
                       font=("Segoe UI", 9)).pack(side="left")

    def _pick_color(self):
        code = colorchooser.askcolor(title="Choose Embed Color", initialcolor=self.color_var.get())
        if code and code[1]:
            self.color_var.set(code[1])

    def add_field(self):
        f = EmbedField(self, self.fields_container, self.fields.remove)
        self.fields.append(f)

    def add_button(self):
        b = ActionButton(self, self.buttons_container, self.buttons.remove)
        self.buttons.append(b)

    def build_payload(self, webhook_username="", webhook_avatar=""):
        payload = {}

        content = self.content_text.get("1.0", "end-1c").strip()
        if content:
            payload["content"] = content

        if webhook_username:
            payload["username"] = webhook_username
        if webhook_avatar:
            payload["avatar_url"] = webhook_avatar

        embed = {}
        if self.title_var.get().strip():
            embed["title"] = self.title_var.get().strip()
        if self.desc_var.get().strip():
            embed["description"] = self.desc_var.get().strip()
        if self.url_var.get().strip():
            embed["url"] = self.url_var.get().strip()

        color_str = self.color_var.get().strip()
        if color_str:
            try:
                embed["color"] = hex_to_int(color_str)
            except ValueError:
                pass

        author_name = self.author_name.get().strip()
        if author_name:
            author = {"name": author_name}
            if self.author_icon.get().strip():
                author["icon_url"] = self.author_icon.get().strip()
            if self.author_url.get().strip():
                author["url"] = self.author_url.get().strip()
            embed["author"] = author

        if self.thumb_var.get().strip():
            embed["thumbnail"] = {"url": self.thumb_var.get().strip()}
        if self.image_var.get().strip():
            embed["image"] = {"url": self.image_var.get().strip()}

        footer_text = self.footer_text.get().strip()
        if footer_text:
            footer = {"text": footer_text}
            if self.footer_icon.get().strip():
                footer["icon_url"] = self.footer_icon.get().strip()
            embed["footer"] = footer

        if self.timestamp_var.get():
            embed["timestamp"] = datetime.utcnow().isoformat() + "Z"

        valid_fields = [f.to_dict() for f in self.fields
                        if f.name_var.get().strip() and f.value_var.get().strip()]
        if valid_fields:
            embed["fields"] = valid_fields

        if embed:
            payload["embeds"] = [embed]

        valid_buttons = [b.to_dict() for b in self.buttons
                         if b.label_var.get().strip() and b.url_var.get().strip()]
        if valid_buttons:
            payload["components"] = [
                {
                    "type": 1,
                    "components": [
                        {"type": 2, "style": 5, "label": btn["label"], "url": btn["url"]}
                        for btn in valid_buttons
                    ]
                }
            ]

        return payload


class WebhookSenderApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Webhook Sender")
        self.root.geometry("1000x750")
        self.root.configure(bg=THEME["bg"])
        self.root.minsize(800, 600)

        self.messages = []
        self.msg_counter = 0

        self._set_icon()
        self._setup_styles()
        self._build_top()
        self._build_main()

        self.add_message()

    def _set_icon(self):
        try:
            self.root.iconbitmap(default="")
        except Exception:
            pass

    def _setup_styles(self):
        style = ttk.Style()
        style.theme_use("clam")
        style.configure("TNotebook", background=THEME["bg"], borderwidth=0)
        style.configure("TNotebook.Tab", background=THEME["section_bg"],
                        foreground=THEME["fg"], padding=[12, 4],
                        font=("Segoe UI", 10))
        style.map("TNotebook.Tab", background=[("selected", THEME["accent"])],
                  foreground=[("selected", "white")])
        style.configure("TFrame", background=THEME["bg"])

    def _make_row(self, parent, label, var, width=50, show=None):
        frame = tk.Frame(parent, bg=THEME["bg"])
        frame.pack(fill="x", pady=(0, 6))
        tk.Label(frame, text=label, bg=THEME["bg"], fg=THEME["fg"],
                 font=("Segoe UI", 10), width=12, anchor="w").pack(side="left")
        entry = tk.Entry(frame, textvariable=var, bg=THEME["input_bg"],
                         fg=THEME["input_fg"], insertbackground=THEME["fg"],
                         relief="flat", font=("Segoe UI", 10), show=show)
        entry.pack(side="left", fill="x", expand=True, padx=(0, 6))
        return frame

    def _build_top(self):
        top = tk.Frame(self.root, bg=THEME["section_bg"], padx=12, pady=10)
        top.pack(fill="x")

        self.webhook_var = tk.StringVar()
        self.username_var = tk.StringVar()
        self.avatar_var = tk.StringVar()

        self._make_row(top, "Webhook URL:", self.webhook_var)
        self._make_row(top, "Username:", self.username_var)
        self._make_row(top, "Avatar URL:", self.avatar_var)

    def _build_main(self):
        main = tk.Frame(self.root, bg=THEME["bg"])
        main.pack(fill="both", expand=True, padx=8, pady=(0, 8))

        left = tk.Frame(main, bg=THEME["section_bg"], width=220)
        left.pack(side="left", fill="y", padx=(0, 8))
        left.pack_propagate(False)

        tk.Label(left, text="Messages", bg=THEME["section_bg"], fg=THEME["accent"],
                 font=("Segoe UI", 12, "bold")).pack(pady=(10, 6))

        self.msg_listbox = tk.Listbox(left, bg=THEME["input_bg"], fg=THEME["fg"],
                                       selectbackground=THEME["accent"],
                                       relief="flat", font=("Segoe UI", 10),
                                       height=15)
        self.msg_listbox.pack(fill="both", expand=True, padx=8)
        self.msg_listbox.bind("<<ListboxSelect>>", self._on_msg_select)

        btn_frame = tk.Frame(left, bg=THEME["section_bg"])
        btn_frame.pack(fill="x", padx=8, pady=8)

        tk.Button(btn_frame, text="+ Add", command=self.add_message,
                  bg=THEME["accent"], fg="white", relief="flat",
                  font=("Segoe UI", 9), cursor="hand2").pack(side="left", fill="x", expand=True, padx=(0, 4))
        tk.Button(btn_frame, text="- Remove", command=self.remove_message,
                  bg=THEME["error"], fg="white", relief="flat",
                  font=("Segoe UI", 9), cursor="hand2").pack(side="left", fill="x", expand=True)

        right = tk.Frame(main, bg=THEME["bg"])
        right.pack(side="left", fill="both", expand=True)

        self.notebook = ttk.Notebook(right)
        self.notebook.pack(fill="both", expand=True)

        bottom = tk.Frame(self.root, bg=THEME["section_bg"], padx=12, pady=8)
        bottom.pack(fill="x")

        self.status_text = scrolledtext.ScrolledText(bottom, bg=THEME["input_bg"],
                                                      fg=THEME["fg"], insertbackground=THEME["fg"],
                                                      relief="flat", font=("Segoe UI", 9),
                                                      height=5, wrap="word")
        self.status_text.pack(fill="x", pady=(0, 6))
        self.status_text.insert("end", "Ready. Enter a webhook URL and compose your message.\n")
        self.status_text.config(state="disabled")

        btn_row = tk.Frame(bottom, bg=THEME["section_bg"])
        btn_row.pack(fill="x")

        tk.Button(btn_row, text="Send Current", command=lambda: self.send(only_current=True),
                  bg=THEME["success"], fg="white", relief="flat",
                  font=("Segoe UI", 10, "bold"), padx=16, pady=4,
                  cursor="hand2").pack(side="left", padx=(0, 8))

        tk.Button(btn_row, text="Send All", command=lambda: self.send(only_current=False),
                  bg=THEME["accent"], fg="white", relief="flat",
                  font=("Segoe UI", 10, "bold"), padx=16, pady=4,
                  cursor="hand2").pack(side="left", padx=(0, 8))

        tk.Button(btn_row, text="Clear Log", command=self.clear_log,
                  bg=THEME["border"], fg=THEME["fg"], relief="flat",
                  font=("Segoe UI", 9), padx=12, pady=4,
                  cursor="hand2").pack(side="right")

    def add_message(self):
        idx = self.msg_counter
        self.msg_counter += 1
        msg = MessageWidget(self, self.notebook, idx, self._remove_msg_ref)
        self.messages.append(msg)
        self.msg_listbox.insert("end", f"Message {idx + 1}")
        self.msg_listbox.selection_clear(0, "end")
        self.msg_listbox.selection_set(self.msg_listbox.size() - 1)
        self.notebook.select(msg.tab)

    def remove_message(self):
        sel = self.msg_listbox.curselection()
        if not sel:
            return
        idx = sel[0]
        if idx < 0 or idx >= len(self.messages):
            return
        msg = self.messages[idx]
        self.notebook.forget(msg.tab)
        self.messages.pop(idx)
        self.msg_listbox.delete(idx)
        for i in range(idx, len(self.messages)):
            self.msg_listbox.delete(i)
            self.msg_listbox.insert(i, f"Message {i + 1}")
        if self.messages:
            sel_idx = min(idx, len(self.messages) - 1)
            self.msg_listbox.selection_set(sel_idx)
            self.notebook.select(self.messages[sel_idx].tab)

    def _remove_msg_ref(self, msg):
        pass

    def _on_msg_select(self, event):
        sel = self.msg_listbox.curselection()
        if sel and sel[0] < len(self.messages):
            self.notebook.select(self.messages[sel[0]].tab)

    def log(self, msg, color=THEME["fg"]):
        self.status_text.config(state="normal")
        tag = f"color_{len(self.status_text.tag_names())}"
        self.status_text.tag_configure(tag, foreground=color)
        self.status_text.insert("end", msg + "\n", tag)
        self.status_text.see("end")
        self.status_text.config(state="disabled")

    def clear_log(self):
        self.status_text.config(state="normal")
        self.status_text.delete("1.0", "end")
        self.status_text.config(state="disabled")

    def send(self, only_current=True):
        url = self.webhook_var.get().strip()
        if not url:
            messagebox.showerror("Error", "Please enter a webhook URL.")
            return

        if not url.startswith("https://discord.com/api/webhooks/"):
            messagebox.showerror("Error", "Invalid webhook URL.\nMust be a Discord webhook URL.")
            return

        username = self.username_var.get().strip()
        avatar = self.avatar_var.get().strip()

        if only_current:
            sel = self.msg_listbox.curselection()
            if not sel:
                messagebox.showerror("Error", "No message selected.")
                return
            targets = [(sel[0], self.messages[sel[0]])]
        else:
            targets = list(enumerate(self.messages))

        def _send():
            for idx, msg in targets:
                payload = msg.build_payload(username, avatar)
                if not payload.get("content") and not payload.get("embeds"):
                    self.log(f"Message {idx + 1}: skipped (empty)", THEME["text_secondary"])
                    continue
                for attempt in range(3):
                    try:
                        resp = requests.post(url, json=payload, timeout=30)
                        if resp.status_code == 204:
                            self.log(f"Message {idx + 1}: sent successfully", THEME["success"])
                            break
                        elif resp.status_code == 429:
                            retry = int(resp.headers.get("Retry-After", 2))
                            self.log(f"Rate limited! Waiting {retry}s (attempt {attempt + 1})...",
                                     THEME["error"])
                            threading.Event().wait(retry)
                        else:
                            self.log(f"Message {idx + 1}: failed ({resp.status_code}) {resp.text[:200]}",
                                     THEME["error"])
                            break
                    except requests.RequestException as e:
                        self.log(f"Message {idx + 1}: error - {e}", THEME["error"])
                        break

        threading.Thread(target=_send, daemon=True).start()


if __name__ == "__main__":
    root = tk.Tk()
    app = WebhookSenderApp(root)
    root.mainloop()
