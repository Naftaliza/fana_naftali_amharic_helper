"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Plus, Pencil, ExternalLink, ArrowRight, ArrowLeft, FileCheck2, Users2, TrendingUp, Layers,
  CheckCircle2, AlertCircle, AlertTriangle, XCircle,
} from "lucide-react";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { useTheme } from "@/lib/theme-context";
import type {
  CreateOrganizationPayload, OrganizationStats, OrganizationSummary, UpdateOrganizationPayload,
} from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";

const CATEGORY_KEYS = [
  "cat.government", "cat.bank", "cat.insurance", "cat.employment",
  "cat.healthcare", "cat.municipality", "cat.other",
];
const URGENCY_KEYS = ["urg.low", "urg.medium", "urg.high", "urg.critical"];

// Validated categorical / status / sequential palette (see the dataviz skill's
// references/palette.md) — deliberately independent of any tenant's brand colors, so usage
// charts stay readable and comparable across every tenant regardless of their own branding.
// Fixed order, never cycled: slot i is always DocumentCategory i.
const CATEGORICAL = {
  light: ["#2a78d6", "#1baf7a", "#eda100", "#008300", "#4a3aa7", "#e34948", "#e87ba4"],
  dark: ["#3987e5", "#199e70", "#c98500", "#008300", "#9085e9", "#e66767", "#d55181"],
};
const STATUS = ["#0ca30c", "#fab219", "#ec835a", "#d03b3b"]; // Low, Medium, High, Critical — same on both surfaces
const STATUS_ICONS = [CheckCircle2, AlertCircle, AlertTriangle, XCircle];
const SEQUENTIAL = { line: { light: "#256abf", dark: "#3987e5" }, area: { light: "#b7d3f6", dark: "#184f95" } };

function slugify(name: string) {
  return name.trim().toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
}

export default function AdminOrganizationsPage() {
  const { t, rtl } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [orgs, setOrgs] = useState<OrganizationSummary[] | null>(null);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<OrganizationSummary | null>(null);
  const [selected, setSelected] = useState<OrganizationSummary | null>(null);
  const [statusBusyId, setStatusBusyId] = useState<string | null>(null);

  useEffect(() => {
    if (loading) return;
    if (!user?.isAdmin) router.replace("/");
  }, [loading, user, router]);

  const reload = () => api.adminListOrganizations().then(setOrgs).catch(() => setOrgs([]));
  useEffect(() => { if (user?.isAdmin) reload(); }, [user]);

  const toggleStatus = async (o: OrganizationSummary) => {
    const next = !o.isActive;
    if (next === false && !window.confirm(t("org.deactivateConfirm"))) return;
    setStatusBusyId(o.id);
    try {
      await api.adminSetOrganizationStatus(o.id, next);
      await reload();
    } finally {
      setStatusBusyId(null);
    }
  };

  if (loading || !user?.isAdmin) {
    return <p className="pt-16 text-center text-gray-500">{t("common.loading")}</p>;
  }

  if (selected) {
    return <StatsView org={selected} onBack={() => setSelected(null)} />;
  }

  return (
    // Breaks out of the global <main max-w-6xl> wrapper so this admin console can use a wider
    // share of the viewport than a normal content page — w-screen + vw-based margins ignore
    // the parent's max-width regardless of its own constraint.
    <div className="w-screen ml-[calc(50%-50vw)] mr-[calc(50%-50vw)] px-4">
    <div className="mx-auto w-[85%] space-y-5 pt-6" dir={rtl ? "rtl" : "ltr"}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{t("org.adminTitle")}</h1>
        {!creating && !editing && (
          <Button onClick={() => setCreating(true)}><Plus className="h-5 w-5" />{t("org.new")}</Button>
        )}
      </div>

      {(creating || editing) && (
        <OrgForm
          org={editing}
          onCancel={() => { setCreating(false); setEditing(null); }}
          onSaved={() => { setCreating(false); setEditing(null); reload(); }}
        />
      )}

      {orgs === null ? (
        <p className="text-gray-500">{t("common.loading")}</p>
      ) : orgs.length === 0 ? (
        <p className="text-gray-500 dark:text-gray-400">{t("org.noOrgs")}</p>
      ) : (
        <ul className="space-y-3">
          {orgs.map((o) => (
            <li key={o.id}>
              <Card>
                <CardContent className="flex flex-col gap-3 py-4">
                  <div className="flex min-w-0 items-start gap-3">
                    <span
                      className="grid h-10 w-10 shrink-0 place-items-center rounded-xl text-sm font-extrabold text-white"
                      style={{ background: `linear-gradient(135deg, ${o.primaryColorHex}, ${o.accentColorHex ?? o.primaryColorHex})` }}
                      aria-hidden="true"
                    >
                      {o.name.trim().charAt(0)}
                    </span>
                    <div className="min-w-0 flex-1 space-y-1.5">
                      <p className="truncate font-semibold">{o.name}</p>
                      {/* Badges get their own wrapping row — inlining them with the name is
                          what caused the pill text itself to break mid-word on narrow screens. */}
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span className="whitespace-nowrap rounded-full bg-brand-light px-2 py-0.5 text-xs text-brand dark:bg-brand/15">
                          {o.slug}
                        </span>
                        <span className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs ${
                          o.isActive
                            ? "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300"
                            : "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400"
                        }`}>
                          {o.isActive ? t("org.active") : t("org.inactive")}
                        </span>
                      </div>
                      <p className="text-sm text-gray-500 dark:text-gray-400">{new Date(o.createdAt).toLocaleDateString()}</p>
                    </div>
                  </div>
                  {/* Full-width stacked buttons on mobile, side by side from sm up — avoids
                      the horizontal overflow a fixed-row layout caused on narrow screens. */}
                  <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
                    <a
                      href={`/?org=${o.slug}`} target="_blank" rel="noopener noreferrer"
                      className="inline-flex items-center justify-center gap-1 rounded-full border border-gray-300 px-3 py-2 text-sm font-medium hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800"
                    >
                      <ExternalLink className="h-4 w-4 shrink-0" />{t("org.previewLink")}
                    </a>
                    <button
                      onClick={() => { setEditing(o); setCreating(false); }}
                      className="inline-flex items-center justify-center gap-1 rounded-full border border-gray-300 px-3 py-2 text-sm font-medium hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800"
                    >
                      <Pencil className="h-4 w-4 shrink-0" />{t("org.edit")}
                    </button>
                    <button
                      onClick={() => toggleStatus(o)}
                      disabled={statusBusyId === o.id}
                      className={`inline-flex items-center justify-center gap-1 rounded-full border px-3 py-2 text-sm font-medium disabled:opacity-50 ${
                        o.isActive
                          ? "border-red-300 text-red-600 hover:bg-red-50 dark:border-red-900/50 dark:hover:bg-red-950/30"
                          : "border-green-300 text-green-700 hover:bg-green-50 dark:border-green-900/50 dark:hover:bg-green-950/30"
                      }`}
                    >
                      {statusBusyId === o.id ? t("common.loading") : o.isActive ? t("org.deactivate") : t("org.reactivate")}
                    </button>
                    <button
                      onClick={() => setSelected(o)}
                      className="inline-flex items-center justify-center gap-1 rounded-full bg-brand px-3 py-2 text-sm font-medium text-white hover:bg-brand-dark"
                    >
                      {t("org.viewStats")}
                    </button>
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
    </div>
  );
}

// ---- Create/edit tenant form. org=null (or omitted) is create mode; passing an existing
// OrganizationSummary switches to edit mode (slug locked — see UpdateOrganizationPayload). ----
function OrgForm({
  org, onCancel, onSaved,
}: { org?: OrganizationSummary | null; onCancel: () => void; onSaved: () => void }) {
  const { t } = useLanguage();
  const [name, setName] = useState(org?.name ?? "");
  const [slug, setSlug] = useState(org?.slug ?? "");
  const [slugTouched, setSlugTouched] = useState(false);
  const [logoUrl, setLogoUrl] = useState(org?.logoUrl ?? "");
  const [primaryColorHex, setPrimaryColorHex] = useState(org?.primaryColorHex ?? "#2563EB");
  const [accentColorHex, setAccentColorHex] = useState(org?.accentColorHex ?? "#0EA5A4");
  const [welcomeText, setWelcomeText] = useState(org?.welcomeText ?? "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      if (org) {
        const body: UpdateOrganizationPayload = {
          name, logoUrl: logoUrl || null, primaryColorHex, accentColorHex: accentColorHex || null, welcomeText,
        };
        await api.adminUpdateOrganization(org.id, body);
      } else {
        const body: CreateOrganizationPayload = {
          name, slug: slug || slugify(name), logoUrl: logoUrl || null,
          primaryColorHex, accentColorHex: accentColorHex || null, welcomeText,
        };
        await api.adminCreateOrganization(body);
      }
      onSaved();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardContent className="space-y-3 py-4">
        <Input
          aria-label={t("org.name")} placeholder={t("org.name")} value={name}
          onChange={(e) => { setName(e.target.value); if (!slugTouched) setSlug(slugify(e.target.value)); }}
        />
        {org ? (
          <div>
            <Input aria-label={t("org.slug")} value={slug} disabled />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("org.slugLocked")}</p>
          </div>
        ) : (
          <div>
            <Input
              aria-label={t("org.slug")} placeholder={t("org.slug")} value={slug}
              onChange={(e) => { setSlug(slugify(e.target.value)); setSlugTouched(true); }}
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">{t("org.slugHint")}</p>
          </div>
        )}
        <textarea
          aria-label={t("org.welcomeText")} placeholder={t("org.welcomeText")} value={welcomeText} rows={2}
          onChange={(e) => setWelcomeText(e.target.value)}
          className="w-full rounded-xl border border-gray-300 bg-white px-4 py-2 text-base focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
        />
        <Input
          aria-label={t("org.logoUrl")} placeholder={t("org.logoUrl")} value={logoUrl}
          onChange={(e) => setLogoUrl(e.target.value)}
        />
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="flex items-center gap-3 rounded-xl border border-gray-300 px-4 py-2 dark:border-gray-700">
            <span className="text-sm text-gray-600 dark:text-gray-300">{t("org.primaryColor")}</span>
            <input type="color" value={primaryColorHex} onChange={(e) => setPrimaryColorHex(e.target.value)} className="h-8 w-12 shrink-0 cursor-pointer" />
          </label>
          <label className="flex items-center gap-3 rounded-xl border border-gray-300 px-4 py-2 dark:border-gray-700">
            <span className="text-sm text-gray-600 dark:text-gray-300">{t("org.accentColor")}</span>
            <input type="color" value={accentColorHex} onChange={(e) => setAccentColorHex(e.target.value)} className="h-8 w-12 shrink-0 cursor-pointer" />
          </label>
        </div>
        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        <div className="flex gap-2">
          <Button onClick={submit} disabled={busy || !name || !welcomeText} className="flex-1">
            {busy ? t("common.loading") : org ? t("org.save") : t("org.create")}
          </Button>
          <button onClick={onCancel} className="rounded-full border border-gray-300 px-4 text-sm font-medium hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800">
            {t("admin.cancel")}
          </button>
        </div>
      </CardContent>
    </Card>
  );
}

// ---- Usage dashboard for one tenant ----
function StatsView({ org, onBack }: { org: OrganizationSummary; onBack: () => void }) {
  const { t, rtl } = useLanguage();
  const { theme } = useTheme();
  const mode = theme === "dark" ? "dark" : "light";
  const [stats, setStats] = useState<OrganizationStats | null>(null);

  useEffect(() => { api.adminOrganizationStats(org.id).then(setStats).catch(() => setStats(null)); }, [org.id]);

  const Back = rtl ? ArrowRight : ArrowLeft;
  const accent = org.accentColorHex ?? org.primaryColorHex;

  // Derived, honestly-computable stats — no fabricated deltas.
  const weeksTracked = stats?.weeklyTrend.length || 1;
  const avgPerWeek = stats ? stats.documentsProcessed / weeksTracked : 0;
  const urgentRate = stats && stats.documentsProcessed > 0 ? Math.round((stats.urgentCount / stats.documentsProcessed) * 100) : 0;
  const topCategory = stats?.byCategory[0]; // backend already orders by Count DESC

  return (
    // Same viewport-breakout as the list view — the dashboard uses ~85% of the page width
    // instead of being squeezed into the global <main max-w-6xl> content column.
    <div className="w-screen ml-[calc(50%-50vw)] mr-[calc(50%-50vw)] px-4">
    <div className="mx-auto w-[85%] space-y-2.5 pb-2 pt-2" dir={rtl ? "rtl" : "ltr"}>
      {/* Header banner — the back button lives inline here (not its own row) to save
          vertical space; echoes the tenant's own identity, tying the admin view to the
          branded front end the same way the Navbar does for a tenant visitor. */}
      <div
        className="flex items-center gap-4 rounded-2xl px-5 py-3 text-white shadow-soft"
        style={{ background: `linear-gradient(135deg, ${org.primaryColorHex}, ${accent})` }}
      >
        <button
          onClick={onBack}
          aria-label={t("org.backToList")}
          className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-white/15 hover:bg-white/25"
        >
          <Back className="h-4 w-4" />
        </button>
        <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-white/20 text-lg font-extrabold">
          {org.name.trim().charAt(0)}
        </span>
        <div className="min-w-0">
          <h1 className="truncate text-xl font-bold leading-tight">{org.name}</h1>
          <p className="text-sm text-white/80">{org.slug} · {org.isActive ? t("org.active") : t("org.inactive")}</p>
        </div>
      </div>

      {stats === null ? (
        <p className="text-gray-500">{t("common.loading")}</p>
      ) : stats.documentsProcessed === 0 ? (
        <p className="text-gray-500 dark:text-gray-400">{t("org.noData")}</p>
      ) : (
        <div className="space-y-3">
          {/* Stat tiles */}
          <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 xl:grid-cols-5">
            <Tile icon={FileCheck2} tint="#2a78d6" label={t("org.documentsProcessed")} value={stats.documentsProcessed.toLocaleString()} />
            <Tile icon={Users2} tint="#1baf7a" label={t("org.uniqueUsers")} value={stats.uniqueUsers.toLocaleString()} />
            <Tile icon={AlertTriangle} tint="#d03b3b" label={t("org.urgentCount")} value={stats.urgentCount.toLocaleString()} sub={`${urgentRate}%`} />
            <Tile icon={TrendingUp} tint="#eda100" label={t("org.avgPerWeek")} value={avgPerWeek.toFixed(1)} />
            {topCategory && (
              <Tile icon={Layers} tint={CATEGORICAL[mode][topCategory.category] ?? CATEGORICAL[mode][6]}
                label={t("org.topCategory")} value={t(CATEGORY_KEYS[topCategory.category] ?? "cat.other")} small />
            )}
          </div>

          {/* Chart + breakdowns in one row on wide screens so nothing stacks unnecessarily. */}
          <div className="grid gap-3 lg:grid-cols-5">
            <Card className="lg:col-span-3">
              <CardContent className="py-3">
                <h2 className="mb-1.5 text-base font-semibold">{t("org.weeklyTrend")}</h2>
                <TrendChart points={stats.weeklyTrend} mode={mode} accent={org.primaryColorHex} />
              </CardContent>
            </Card>

            <div className="space-y-2.5 lg:col-span-2">
              {stats.byCategory.length > 0 && (
                <Card>
                  <CardContent className="py-3">
                    <h2 className="mb-2 text-base font-semibold">{t("org.byCategory")}</h2>
                    <CategoryBars data={stats.byCategory} mode={mode} t={t} total={stats.documentsProcessed} />
                  </CardContent>
                </Card>
              )}
              {stats.byUrgency.length > 0 && (
                <Card>
                  <CardContent className="py-3">
                    <h2 className="mb-2 text-base font-semibold">{t("org.byUrgency")}</h2>
                    <UrgencyRows data={stats.byUrgency} t={t} />
                  </CardContent>
                </Card>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
    </div>
  );
}

function Tile({
  icon: Icon, tint, label, value, sub, small,
}: { icon: typeof FileCheck2; tint: string; label: string; value: string; sub?: string; small?: boolean }) {
  return (
    <Card className="overflow-hidden">
      <CardContent className="flex items-center gap-3 py-3">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-lg" style={{ background: `${tint}1a`, color: tint }}>
          <Icon className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <p className="truncate text-xs font-medium leading-tight text-gray-500 dark:text-gray-400">{label}</p>
          <div className="flex items-baseline gap-1.5">
            <p className={`truncate font-bold tabular-nums ${small ? "text-base" : "text-2xl"}`}>{value}</p>
            {sub && <span className="text-xs font-semibold text-gray-400 dark:text-gray-500">({sub})</span>}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function CategoryBars({
  data, mode, t, total,
}: { data: { category: number; count: number }[]; mode: "light" | "dark"; t: (k: string) => string; total: number }) {
  const max = Math.max(...data.map((d) => d.count), 1);
  const colors = CATEGORICAL[mode];
  // Cap rows so this stays compact regardless of how many categories are in play —
  // the rest fold into an "other" tally rather than pushing the page taller.
  const shown = data.slice(0, 4);
  const rest = data.slice(4);
  const restCount = rest.reduce((s, d) => s + d.count, 0);

  return (
    <div className="space-y-2">
      {shown.map((d) => (
        <div key={d.category} className="flex items-center gap-2.5">
          <span className="w-28 shrink-0 truncate text-end text-sm text-gray-600 dark:text-gray-300">
            {t(CATEGORY_KEYS[d.category] ?? "cat.other")}
          </span>
          <span className="h-2.5 flex-1 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
            <span
              className="block h-full rounded-full"
              style={{ width: `${(d.count / max) * 100}%`, background: colors[d.category] ?? colors[colors.length - 1] }}
            />
          </span>
          <span className="w-16 shrink-0 text-end text-sm tabular-nums">
            <span className="font-semibold">{d.count}</span>
            <span className="text-gray-400 dark:text-gray-500"> · {Math.round((d.count / total) * 100)}%</span>
          </span>
        </div>
      ))}
      {rest.length > 0 && (
        <p className="text-end text-xs text-gray-400 dark:text-gray-500">
          +{rest.length} {t("cat.other")} ({restCount})
        </p>
      )}
    </div>
  );
}

function UrgencyRows({ data, t }: { data: { urgency: number; count: number }[]; t: (k: string) => string }) {
  const total = data.reduce((s, d) => s + d.count, 0) || 1;
  // Fixed Low->Critical order regardless of what the API returned.
  const ordered = [0, 1, 2, 3].map((u) => data.find((d) => d.urgency === u)?.count ?? 0);

  return (
    <div className="space-y-2">
      <div className="flex h-2.5 gap-0.5 overflow-hidden rounded-full">
        {ordered.map((count, u) => (
          count > 0 && (
            <span key={u} style={{ width: `${(count / total) * 100}%`, background: STATUS[u] }} />
          )
        ))}
      </div>
      <div className="grid gap-1.5">
        {ordered.map((count, u) => {
          const Icon = STATUS_ICONS[u];
          return (
            <div key={u} className="flex items-center gap-2.5 text-sm">
              <span className="grid h-5 w-5 shrink-0 place-items-center rounded-full text-white" style={{ background: STATUS[u] }}>
                <Icon className="h-3 w-3" />
              </span>
              <span className="flex-1 text-gray-600 dark:text-gray-300">{t(URGENCY_KEYS[u])}</span>
              <span className="tabular-nums">
                <span className="font-semibold">{count}</span>
                <span className="text-gray-400 dark:text-gray-500"> · {Math.round((count / total) * 100)}%</span>
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// Catmull-Rom -> cubic-Bezier smoothing (tension 1/6) — a standard, cheap way to turn a
// straight-segment polyline into a smooth curve without pulling in a charting library.
function smoothPath(pts: { x: number; y: number }[]): string {
  if (pts.length < 2) return "";
  if (pts.length === 2) return `M ${pts[0].x},${pts[0].y} L ${pts[1].x},${pts[1].y}`;
  let d = `M ${pts[0].x},${pts[0].y}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] ?? pts[i];
    const p1 = pts[i];
    const p2 = pts[i + 1];
    const p3 = pts[i + 2] ?? p2;
    const c1x = p1.x + (p2.x - p0.x) / 6, c1y = p1.y + (p2.y - p0.y) / 6;
    const c2x = p2.x - (p3.x - p1.x) / 6, c2y = p2.y - (p3.y - p1.y) / 6;
    d += ` C ${c1x},${c1y} ${c2x},${c2y} ${p2.x},${p2.y}`;
  }
  return d;
}

function TrendChart({ points, mode, accent }: { points: { weekStart: string; count: number }[]; mode: "light" | "dark"; accent: string }) {
  const { t } = useLanguage();
  const [hover, setHover] = useState<number | null>(null);
  const gradId = "trend-fill";

  if (points.length === 0) return <p className="text-sm text-gray-500 dark:text-gray-400">{t("org.noData")}</p>;

  const W = 480, H = 128, TOP = 18, BOTTOM = 92, LEFT = 4, RIGHT = 476;
  const counts = points.map((p) => p.count);
  const max = Math.max(...counts, 1);
  // Pad the ceiling above the max so a flat-at-max series still reads as a line, not a
  // block filling the whole plot — standard practice: data should never touch the top edge.
  const ceiling = Math.max(max * 1.2, max + 1);
  const n = points.length;
  const x = (i: number) => (n === 1 ? (LEFT + RIGHT) / 2 : LEFT + (i / (n - 1)) * (RIGHT - LEFT));
  const y = (v: number) => BOTTOM - (v / ceiling) * (BOTTOM - TOP);
  const pts = points.map((p, i) => ({ x: x(i), y: y(p.count) }));
  const line = smoothPath(pts);
  const area = n > 1 ? `${line} L ${pts[n - 1].x},${BOTTOM} L ${pts[0].x},${BOTTOM} Z` : "";
  const last = points[n - 1];
  const grid = mode === "dark" ? "#2c2c2a" : "#e9e8e3";
  const muted = "#8b8a85";
  const active = hover ?? n - 1;

  const onMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const px = ((e.clientX - rect.left) / rect.width) * W;
    let nearest = 0, best = Infinity;
    pts.forEach((p, i) => { const d = Math.abs(p.x - px); if (d < best) { best = d; nearest = i; } });
    setHover(nearest);
  };

  return (
    <svg
      viewBox={`0 0 ${W} ${H}`} className="w-full cursor-crosshair" role="img" aria-label={t("org.weeklyTrend")}
      onMouseMove={onMove} onMouseLeave={() => setHover(null)}
    >
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={accent} stopOpacity={0.28} />
          <stop offset="100%" stopColor={accent} stopOpacity={0} />
        </linearGradient>
      </defs>

      {/* Recessive reference gridlines with value labels */}
      {[0.5, 1].map((frac) => (
        <g key={frac}>
          <line x1={LEFT} y1={y(ceiling * frac)} x2={RIGHT} y2={y(ceiling * frac)} stroke={grid} strokeWidth={1} />
          <text x={RIGHT} y={y(ceiling * frac) - 4} fontSize={10} textAnchor="end" fill={muted}>
            {Math.round(ceiling * frac)}
          </text>
        </g>
      ))}
      <line x1={LEFT} y1={BOTTOM} x2={RIGHT} y2={BOTTOM} stroke={mode === "dark" ? "#3a3a38" : "#d8d7d2"} strokeWidth={1} />

      {n > 1 && (
        <>
          <path d={area} fill={`url(#${gradId})`} />
          <path d={line} fill="none" stroke={accent} strokeWidth={2.25} strokeLinecap="round" strokeLinejoin="round" />
        </>
      )}

      {/* Hover crosshair + point */}
      {hover !== null && (
        <line x1={pts[hover].x} y1={TOP} x2={pts[hover].x} y2={BOTTOM} stroke={accent} strokeWidth={1} strokeDasharray="3 3" opacity={0.5} />
      )}
      {pts.map((p, i) => (
        <circle
          key={i} cx={p.x} cy={p.y} r={i === active ? 4.5 : 0} fill={accent}
          stroke={mode === "dark" ? "#17223B" : "#fff"} strokeWidth={2}
          className="transition-[r]"
        />
      ))}

      {/* Tooltip for the active (hovered, or last by default) point */}
      {(() => {
        const p = pts[active];
        const d = points[active];
        const label = new Date(d.weekStart).toLocaleDateString(undefined, { month: "short", day: "numeric" });
        const boxW = 74, boxH = 34;
        const bx = Math.min(Math.max(p.x - boxW / 2, LEFT), RIGHT - boxW);
        const by = Math.max(p.y - boxH - 10, TOP);
        return (
          <g pointerEvents="none">
            <rect x={bx} y={by} width={boxW} height={boxH} rx={8} fill={mode === "dark" ? "#111827" : "#16233F"} opacity={0.95} />
            <text x={bx + boxW / 2} y={by + 14} fontSize={10} textAnchor="middle" fill="#cbd5e1">{label}</text>
            <text x={bx + boxW / 2} y={by + 27} fontSize={12} fontWeight={700} textAnchor="middle" fill="#fff">{d.count}</text>
          </g>
        );
      })()}

      <text x={LEFT} y={H - 6} fontSize={10} fill={muted}>
        {new Date(points[0].weekStart).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
      </text>
      <text x={RIGHT} y={H - 6} fontSize={10} textAnchor="end" fill={muted}>
        {new Date(last.weekStart).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
      </text>
    </svg>
  );
}
