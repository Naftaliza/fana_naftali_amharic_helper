"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { FileText, Menu, X, Check, LayoutDashboard, User, LogOut, LogIn, UserPlus, Sun, Moon } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { useTheme } from "@/lib/theme-context";
import { LANGUAGES } from "@/lib/types";

export function Navbar() {
  const { t, language, setLanguage } = useLanguage();
  const { theme, toggle } = useTheme();
  const { user, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const pathname = usePathname();

  // Close the menu on outside click, on Escape, and whenever the route changes.
  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, []);
  useEffect(() => setOpen(false), [pathname]);

  const itemClass =
    "flex w-full items-center gap-3 px-4 py-3 text-start text-base text-gray-700 hover:bg-gray-50 dark:text-gray-200 dark:hover:bg-gray-800";

  return (
    <header className="pt-safe sticky top-0 z-40 border-b border-gray-100 bg-white/85 backdrop-blur dark:border-gray-800 dark:bg-gray-900/85">
      <nav aria-label={t("app.name")} className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2 text-lg font-bold text-brand">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-brand-gradient text-white" aria-hidden="true">
            <FileText className="h-5 w-5" />
          </span>
          <span>{t("app.name")}</span>
        </Link>

        <div ref={ref} className="relative">
          <button
            onClick={() => setOpen((o) => !o)}
            aria-label={t("nav.menu")}
            aria-haspopup="menu"
            aria-expanded={open}
            className="grid h-11 w-11 place-items-center rounded-xl border border-gray-200 bg-white text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-200 dark:hover:bg-gray-800"
          >
            {open ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
          </button>

          {/* Dim backdrop — tapping the grayed-out area closes the menu. */}
          {open && (
            <button
              type="button"
              aria-hidden="true"
              tabIndex={-1}
              onClick={() => setOpen(false)}
              className="fixed inset-0 z-40 bg-black/30 backdrop-blur-[1px] animate-fade-in"
            />
          )}

          {open && (
            <div className="absolute end-0 z-50 mt-2 w-64 overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-soft dark:border-gray-800 dark:bg-gray-900">
              {/* Language preference */}
              <p className="px-4 pt-3 pb-1 text-xs font-semibold uppercase tracking-wide text-gray-400">
                {t("nav.language")}
              </p>
              {LANGUAGES.map((l) => (
                <button
                  key={l.code}
                  onClick={() => { setLanguage(l.code); setOpen(false); }}
                  className={`${itemClass} justify-between ${language === l.code ? "text-brand" : ""}`}
                >
                  <span className="flex items-center gap-3">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={`https://flagcdn.com/24x18/${l.flag}.png`}
                      srcSet={`https://flagcdn.com/48x36/${l.flag}.png 2x`}
                      width={24}
                      height={18}
                      alt=""
                      aria-hidden="true"
                      className="rounded-sm shadow-sm"
                    />
                    {l.label}
                  </span>
                  {language === l.code && <Check className="h-4 w-4" />}
                </button>
              ))}

              <div className="my-1 border-t border-gray-100 dark:border-gray-800" />

              {/* Account / navigation */}
              {user ? (
                <>
                  <Link href="/dashboard" className={itemClass}>
                    <LayoutDashboard className="h-5 w-5 text-brand" />{t("nav.dashboard")}
                  </Link>
                  <Link href="/profile" className={itemClass}>
                    <User className="h-5 w-5 text-brand" />{t("nav.profile")}
                  </Link>
                  <button onClick={() => { setOpen(false); logout(); }} className={`${itemClass} text-red-600`}>
                    <LogOut className="h-5 w-5" />{t("nav.logout")}
                  </button>
                </>
              ) : (
                <>
                  <Link href="/login" className={itemClass}>
                    <LogIn className="h-5 w-5 text-brand" />{t("nav.login")}
                  </Link>
                  <Link href="/register" className={itemClass}>
                    <UserPlus className="h-5 w-5 text-brand" />{t("nav.register")}
                  </Link>
                </>
              )}

              <div className="my-1 border-t border-gray-100 dark:border-gray-800" />

              {/* Theme toggle */}
              <button onClick={toggle} className={`${itemClass} justify-between`}>
                <span className="flex items-center gap-3">
                  {theme === "dark" ? <Moon className="h-5 w-5 text-brand" /> : <Sun className="h-5 w-5 text-brand" />}
                  {t("nav.theme")}
                </span>
                <span className="text-sm text-gray-400">{theme === "dark" ? t("theme.dark") : t("theme.light")}</span>
              </button>
            </div>
          )}
        </div>
      </nav>
    </header>
  );
}
