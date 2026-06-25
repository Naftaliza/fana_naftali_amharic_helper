"use client";

import { useEffect, useRef, useState } from "react";
import { useParams } from "next/navigation";
import { Send } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { LANGUAGE_ENUM } from "@/lib/types";
import type { ChatMessage } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default function ChatPage() {
  const { t, language } = useLanguage();
  const params = useParams();
  const id = params.id as string;
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => { api.chatHistory(id).then(setMessages).catch(() => {}); }, [id]);
  useEffect(() => { bottomRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages]);

  const send = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!question.trim()) return;
    const q = question;
    setQuestion("");
    setBusy(true);
    // Optimistically render the user's message.
    setMessages((m) => [...m, { id: `tmp-${Date.now()}`, role: 0, content: q, createdAt: new Date().toISOString() }]);
    try {
      const reply = await api.sendChat(id, q, LANGUAGE_ENUM[language]);
      setMessages((m) => [...m, reply]);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto flex h-[calc(100dvh-11rem)] max-w-2xl flex-col sm:h-[calc(100dvh-9rem)]">
      <h1 className="mb-4 text-2xl font-bold">{t("doc.chat")}</h1>

      <div className="flex-1 space-y-3 overflow-y-auto rounded-2xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
        {messages.map((m) => (
          <div key={m.id} className={`flex ${m.role === 0 ? "justify-end" : "justify-start"}`}>
            <div className={`max-w-[80%] whitespace-pre-wrap rounded-2xl px-4 py-2 ${
              m.role === 0 ? "bg-brand text-white" : "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-100"
            }`}>
              {m.content}
            </div>
          </div>
        ))}
        {busy && <p role="status" className="text-sm text-gray-400 dark:text-gray-500">{t("common.loading")}</p>}
        <div ref={bottomRef} />
      </div>

      <form onSubmit={send} className="mt-3 flex gap-2">
        <Input value={question} onChange={(e) => setQuestion(e.target.value)} aria-label={t("chat.placeholder")} placeholder={t("chat.placeholder")} />
        <Button type="submit" disabled={busy}><Send className="h-5 w-5" />{t("chat.send")}</Button>
      </form>
    </div>
  );
}
