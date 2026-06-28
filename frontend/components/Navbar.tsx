"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { FileText, Menu, X, Check, LayoutDashboard, User, LogOut, LogIn, UserPlus, Sun, Moon, Globe, Briefcase, ShieldCheck } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { useTheme } from "@/lib/theme-context";
import { LANGUAGES } from "@/lib/types";

function Flag({ code, size = 24 }: { code: string; size?: number }) {
  const h = Math.round((size / 4) * 3);
  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`https://flagcdn.com/${size}x${h}/${code}.png`}
      srcSet={`https://flagcdn.com/${size * 2}x${h * 2}/${code}.png 2x`}
      width={size}
      height={h}
      alt=""
      aria-hidden="true"
      className="rounded-sm shadow-sm"
    />
  );
}

export function Navbar() {
  const { t, language, setLanguage } = useLanguage();
  const { theme, toggle } = useTheme();
  const { user, logout } = useAuth();
  const [open, setOpen] = useState(false);       // hamburger menu
  const [langOpen, setLangOpen] = useState(false); // language popover
  const menuRef = useRef<HTMLDivElement>(null);
  const langRef = useRef<HTMLDivElement>(null);
  const pathname = usePathname();

  const current = LANGUAGES.find((l) => l.code === language) ?? LANGUAGES[0];

  // Close both popovers on outside click, Escape, and route change.
  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      const n = e.target as Node;
      if (menuRef.current && !menuRef.current.contains(n)) setOpen(false);
      if (langRef.current && !langRef.current.contains(n)) setLangOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") { setOpen(false); setLangOpen(false); }
    };
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, []);
  useEffect(() => { setOpen(false); setLangOpen(false); }, [pathname]);

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

        <div className="flex items-center gap-2">
          {/* Language picker */}
          <div ref={langRef} className="relative">
            <button
              onClick={() => { setLangOpen((o) => !o); setOpen(false); }}
              aria-label={t("nav.language")}
              aria-haspopup="menu"
              aria-expanded={langOpen}
              className="flex h-11 items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-2.5 text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-200 dark:hover:bg-gray-800"
            >
              <Flag code={current.flag} size={24} />
              <Globe className="h-4 w-4 text-gray-400" />
            </button>

            {langOpen && (
              <button
                type="button"
                aria-hidden="true"
                tabIndex={-1}
                onClick={() => setLangOpen(false)}
                className="fixed inset-0 z-40 bg-black/30 backdrop-blur-[1px] animate-fade-in"
              />
            )}

            {langOpen && (
              <div role="menu" className="absolute end-0 z-50 mt-2 w-52 overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-soft dark:border-gray-800 dark:bg-gray-900">
                {LANGUAGES.map((l) => (
                  <button
                    key={l.code}
                    onClick={() => { setLanguage(l.code); setLangOpen(false); }}
                    className={`${itemClass} justify-between ${language === l.code ? "text-brand" : ""}`}
                  >
                    <span className="flex items-center gap-3">
                      <Flag code={l.flag} size={24} />
                      {l.label}
                    </span>
                    {language === l.code && <Check className="h-4 w-4" />}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Hamburger menu */}
          <div ref={menuRef} className="relative">
            <button
              onClick={() => { setOpen((o) => !o); setLangOpen(false); }}
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

                {/* For businesses — public referral signup */}
                <Link href="/partners" className={itemClass}>
                  <Briefcase className="h-5 w-5 text-brand" />{t("nav.partners")}
                </Link>
                {user?.isAdmin && (
                  <Link href="/admin/providers" className={itemClass}>
                    <ShieldCheck className="h-5 w-5 text-brand" />{t("nav.providerReview")}
                  </Link>
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
        </div>
      </nav>
    </header>
  );
}
