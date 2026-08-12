"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Camera, HelpCircle, LayoutDashboard, User } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";

/**
 * Primary navigation — a bottom tab bar on mobile, an inline row under the header on desktop
 * (same markup, different position via the md: breakpoint). Before this, every destination in
 * the app lived inside the hamburger menu, including /upload — the app's whole purpose — which
 * had no entry point anywhere outside one dashboard button. Four destinations only: adding more
 * would recreate the hamburger's discoverability problem at a different z-index.
 *
 * Tab labels reuse existing nav.* dictionary keys (already at full he/am/en parity) rather than
 * introducing new ones for the same words.
 */
export function BottomNav() {
  const { t } = useLanguage();
  const { user } = useAuth();
  const pathname = usePathname();

  const homeHref = user ? "/dashboard" : "/";
  const meHref = user ? "/profile" : "/login";

  const isHome = pathname === "/dashboard" || (!user && pathname === "/");
  const isUpload = pathname.startsWith("/upload");
  const isHelp = pathname.startsWith("/help");
  const isMe = pathname.startsWith("/profile") || (!user && pathname.startsWith("/login"));

  const tabs = [
    { href: homeHref, label: t("nav.dashboard"), icon: LayoutDashboard, active: isHome },
    { href: "/upload", label: t("nav.upload"), icon: Camera, active: isUpload, primary: true },
    { href: "/help", label: t("nav.help"), icon: HelpCircle, active: isHelp },
    { href: meHref, label: user ? t("nav.profile") : t("nav.login"), icon: User, active: isMe },
  ];

  return (
    <nav
      data-print-hide
      data-a11y-fixed
      aria-label={t("nav.menu")}
      // Always position:fixed (never sticky) — bottom on mobile, top-under-Navbar on desktop —
      // so its DOM placement in layout.tsx doesn't matter for either breakpoint's layout, which
      // is what lets it live outside the grayscale scope wrapper (see that comment in layout.tsx)
      // without a sticky-flow ordering conflict.
      className="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-gray-100 bg-white/95 backdrop-blur dark:border-gray-800 dark:bg-gray-900/95 md:bottom-auto md:top-16 md:border-b md:border-t-0 md:pb-0"
    >
      <div className="mx-auto flex max-w-6xl items-stretch justify-around md:justify-center md:gap-10">
        {tabs.map(({ href, label, icon: Icon, active, primary }) => (
          <Link
            key={label}
            href={href}
            aria-current={active ? "page" : undefined}
            className="flex min-w-[4.25rem] flex-col items-center justify-center gap-1 py-2 md:min-w-0 md:flex-row md:gap-2 md:py-3"
          >
            <span
              className={`grid h-9 w-9 place-items-center rounded-full ${
                primary
                  ? "bg-brand-gradient text-white"
                  : active
                    ? "bg-brand-light text-brand dark:bg-brand/20"
                    : "text-gray-500 dark:text-gray-400"
              }`}
            >
              <Icon className="h-5 w-5" />
            </span>
            <span
              className={`text-xs ${
                active || primary ? "font-semibold text-brand" : "font-medium text-gray-500 dark:text-gray-400"
              }`}
            >
              {label}
            </span>
          </Link>
        ))}
      </div>
    </nav>
  );
}
