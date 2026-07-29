# Offline Handling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make "no connection" a visible, distinct state instead of silently masquerading as "you have no documents" or a spinner that never resolves — a global offline banner, a dashboard that tells offline apart from empty, and capture/upload disabled (with an explanation) while offline.

**Architecture:** One small reactive hook (`useOnlineStatus`) wraps the browser's `online`/`offline` events; a global `OfflineBanner` singleton (same tier as `LanguageGate`/`Onboarding` in `app/layout.tsx`) surfaces it everywhere. The dashboard's silent `.catch(() => {})` becomes a real error state with a retry button, distinguished from the genuine empty-list case. `UploadExperience` disables its two capture entry points while offline. No service-worker changes — `public/sw.js`'s existing cache-then-network-fallback behavior for navigations is exactly why the app shell loads at all when offline, and is unrelated to distinguishing offline from empty data, which is a page-level concern.

**Tech Stack:** Next.js/React, Jest + @testing-library/react (`renderHook`/`act` from `@testing-library/react` itself — this project's RTL version ships them, no separate `@testing-library/react-hooks` package needed).

---

## File Structure

- Create: `frontend/lib/useOnlineStatus.ts`
- Create: `frontend/lib/__tests__/useOnlineStatus.test.ts`
- Create: `frontend/components/OfflineBanner.tsx`
- Create: `frontend/components/__tests__/OfflineBanner.test.tsx`
- Modify: `frontend/app/layout.tsx` — render `OfflineBanner` alongside the other global singletons.
- Modify: `frontend/app/dashboard/page.tsx` — replace the swallowed catch with a visible offline/error state + retry.
- Modify: `frontend/components/UploadExperience.tsx` — disable capture/upload while offline, with an explanatory line.
- Modify: `frontend/i18n/dictionaries.ts` — add `offline.*` keys.

---

### Task 1: `useOnlineStatus` hook

**Files:**
- Create: `frontend/lib/useOnlineStatus.ts`
- Test: `frontend/lib/__tests__/useOnlineStatus.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// frontend/lib/__tests__/useOnlineStatus.test.ts
import { renderHook, act } from "@testing-library/react";
import { useOnlineStatus } from "@/lib/useOnlineStatus";

describe("useOnlineStatus", () => {
  afterEach(() => {
    Object.defineProperty(window.navigator, "onLine", { value: true, configurable: true });
  });

  it("reflects navigator.onLine on first render", () => {
    Object.defineProperty(window.navigator, "onLine", { value: false, configurable: true });
    const { result } = renderHook(() => useOnlineStatus());
    expect(result.current).toBe(false);
  });

  it("flips to false when the offline event fires", () => {
    const { result } = renderHook(() => useOnlineStatus());
    expect(result.current).toBe(true);
    act(() => window.dispatchEvent(new Event("offline")));
    expect(result.current).toBe(false);
  });

  it("flips back to true when the online event fires", () => {
    const { result } = renderHook(() => useOnlineStatus());
    act(() => window.dispatchEvent(new Event("offline")));
    act(() => window.dispatchEvent(new Event("online")));
    expect(result.current).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest lib/__tests__/useOnlineStatus.test.ts`
Expected: FAIL — `Cannot find module '@/lib/useOnlineStatus'`

- [ ] **Step 3: Implement**

```ts
// frontend/lib/useOnlineStatus.ts
"use client";

import { useEffect, useState } from "react";

/**
 * Tracks browser connectivity via the online/offline events. Starts from navigator.onLine so a
 * page loaded while already offline (the service worker's cached app shell still renders fine,
 * see public/sw.js) reflects that immediately rather than assuming online for one render.
 */
export function useOnlineStatus(): boolean {
  const [online, setOnline] = useState(() => (typeof navigator === "undefined" ? true : navigator.onLine));

  useEffect(() => {
    const goOnline = () => setOnline(true);
    const goOffline = () => setOnline(false);
    window.addEventListener("online", goOnline);
    window.addEventListener("offline", goOffline);
    return () => {
      window.removeEventListener("online", goOnline);
      window.removeEventListener("offline", goOffline);
    };
  }, []);

  return online;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest lib/__tests__/useOnlineStatus.test.ts`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/lib/useOnlineStatus.ts frontend/lib/__tests__/useOnlineStatus.test.ts
git commit -m "feat: add useOnlineStatus hook tracking browser connectivity"
```

---

### Task 2: `offline.*` translation keys

**Files:**
- Modify: `frontend/i18n/dictionaries.ts`

- [ ] **Step 1: Add to the Hebrew block**

```ts
  "offline.banner": "אין חיבור לאינטרנט. חלק מהפעולות לא יעבדו עד לחזרת החיבור.",
  "offline.dashboardError": "אי אפשר לטעון את המסמכים שלכם ללא חיבור לאינטרנט.",
  "offline.retry": "נסו שוב",
  "offline.uploadDisabled": "צילום והעלאה דורשים חיבור לאינטרנט.",
```

- [ ] **Step 2: Add to the Amharic block**

```ts
  "offline.banner": "የበይነመረብ ግንኙነት የለም። ግንኙነቱ እስኪመለስ ድረስ አንዳንድ ተግባራት አይሰሩም።",
  "offline.dashboardError": "ያለ በይነመረብ ግንኙነት ሰነዶችዎን መጫን አልተቻለም።",
  "offline.retry": "እንደገና ይሞክሩ",
  "offline.uploadDisabled": "ፎቶ ማንሳት እና መስቀል የበይነመረብ ግንኙነት ይፈልጋል።",
```

- [ ] **Step 3: Add to the English block**

```ts
  "offline.banner": "No internet connection. Some actions won't work until it's back.",
  "offline.dashboardError": "Couldn't load your documents without an internet connection.",
  "offline.retry": "Try again",
  "offline.uploadDisabled": "Photographing and uploading need an internet connection.",
```

- [ ] **Step 4: Commit**

```bash
git add frontend/i18n/dictionaries.ts
git commit -m "feat: add offline.* translation keys"
```

---

### Task 3: `OfflineBanner` component + wire into `layout.tsx`

**Files:**
- Create: `frontend/components/OfflineBanner.tsx`
- Test: `frontend/components/__tests__/OfflineBanner.test.tsx`
- Modify: `frontend/app/layout.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/OfflineBanner.test.tsx
import { render, screen, act } from "@testing-library/react";
import { OfflineBanner } from "@/components/OfflineBanner";
import { LanguageProvider } from "@/lib/language-context";

function renderBanner() {
  window.localStorage.setItem("lang", "en");
  return render(<LanguageProvider><OfflineBanner /></LanguageProvider>);
}

describe("OfflineBanner", () => {
  afterEach(() => {
    Object.defineProperty(window.navigator, "onLine", { value: true, configurable: true });
  });

  it("renders nothing while online", () => {
    const { container } = renderBanner();
    expect(container).toBeEmptyDOMElement();
  });

  it("shows a status banner when the offline event fires", () => {
    renderBanner();
    act(() => window.dispatchEvent(new Event("offline")));
    expect(screen.getByRole("status")).toHaveTextContent(/No internet connection/);
  });

  it("hides again once back online", () => {
    const { container } = renderBanner();
    act(() => window.dispatchEvent(new Event("offline")));
    act(() => window.dispatchEvent(new Event("online")));
    expect(container).toBeEmptyDOMElement();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/OfflineBanner.test.tsx`
Expected: FAIL — `Cannot find module '@/components/OfflineBanner'`

- [ ] **Step 3: Implement**

```tsx
// frontend/components/OfflineBanner.tsx
"use client";

import { WifiOff } from "lucide-react";
import { useOnlineStatus } from "@/lib/useOnlineStatus";
import { useLanguage } from "@/lib/language-context";

/**
 * Global connectivity banner. The service worker's cached app shell (public/sw.js) means the app
 * still loads and looks normal with no connection — this is the one place that says otherwise,
 * so a user isn't left guessing why every action is quietly failing.
 */
export function OfflineBanner() {
  const online = useOnlineStatus();
  const { t, rtl } = useLanguage();

  if (online) return null;

  return (
    <div
      role="status"
      dir={rtl ? "rtl" : "ltr"}
      className="flex items-center justify-center gap-2 bg-amber-500 px-4 py-2 text-center text-sm font-medium text-white"
    >
      <WifiOff className="h-4 w-4 shrink-0" />
      {t("offline.banner")}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/OfflineBanner.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 5: Wire into `layout.tsx`**

Add the import:

```tsx
import { OfflineBanner } from "@/components/OfflineBanner";
```

Render it right after `<Navbar />` (`layout.tsx:66-67`) so it's anchored at the top, above `<main>`:

```tsx
                  <SkipLink />
                  <Navbar />
                  <OfflineBanner />
                  <main id="main" className="mx-auto max-w-6xl px-4 py-8">{children}</main>
```

- [ ] **Step 6: Commit**

```bash
git add frontend/components/OfflineBanner.tsx frontend/components/__tests__/OfflineBanner.test.tsx frontend/app/layout.tsx
git commit -m "feat: add global OfflineBanner"
```

---

### Task 4: Dashboard — distinguish offline/error from empty, add retry

**Files:**
- Modify: `frontend/app/dashboard/page.tsx`
- Test: `frontend/app/dashboard/__tests__/page.test.tsx` (created by the confirm-dialog plan's Task 3 — extend it; create fresh with the same mocking pattern if that plan hasn't run yet)

Current effect (`dashboard/page.tsx:40-42`):

```tsx
  useEffect(() => {
    if (user) api.listDocuments().then(setDocs).catch(() => {}).finally(() => setFetching(false));
  }, [user]);
```

Current empty-state render (`dashboard/page.tsx:51-56`):

```tsx
      {fetching ? (
        <p className="text-gray-500">{t("common.loading")}</p>
      ) : docs.length === 0 ? (
        <Card><CardContent className="py-12 text-center text-gray-500 dark:text-gray-400">
          {t("upload.drop")}
        </CardContent></Card>
      ) : (
```

- [ ] **Step 1: Write the failing tests (append to `page.test.tsx`)**

```tsx
  it("shows a distinct error with retry when the document list fails to load", async () => {
    (api.listDocuments as jest.Mock).mockRejectedValue(new Error("NETWORK_ERROR"));
    renderPage();
    await waitFor(() => screen.getByRole("alert"));
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
    expect(screen.queryByText(DOC.fileName)).not.toBeInTheDocument();
  });

  it("retries the fetch when 'Try again' is clicked", async () => {
    (api.listDocuments as jest.Mock)
      .mockRejectedValueOnce(new Error("NETWORK_ERROR"))
      .mockResolvedValueOnce([DOC]);
    renderPage();
    await waitFor(() => screen.getByRole("alert"));

    fireEvent.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => screen.getByText("letter.pdf"));
    expect(api.listDocuments).toHaveBeenCalledTimes(2);
  });

  it("still shows the plain empty state (not an error) when the list genuinely has zero documents", async () => {
    (api.listDocuments as jest.Mock).mockResolvedValue([]);
    renderPage();
    await waitFor(() => expect(screen.queryByText("common.loading")).not.toBeInTheDocument());
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
```

(Add `import { fireEvent } from "@testing-library/react"` to the file's existing RTL import if not already present.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest app/dashboard/__tests__/page.test.tsx`
Expected: FAIL — a failed fetch currently leaves `docs` empty with no `role="alert"`, indistinguishable from a genuine empty list.

- [ ] **Step 3: Implement**

Add a `loadError` state and a `reload` function, replacing the effect:

```tsx
  const [loadError, setLoadError] = useState(false);

  const reload = () => {
    setFetching(true);
    setLoadError(false);
    api.listDocuments()
      .then(setDocs)
      .catch(() => setLoadError(true))
      .finally(() => setFetching(false));
  };

  useEffect(() => {
    if (user) reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user]);
```

Replace the render branch:

```tsx
      {fetching ? (
        <p className="text-gray-500">{t("common.loading")}</p>
      ) : loadError ? (
        <Card><CardContent className="flex flex-col items-center gap-3 py-12 text-center">
          <p role="alert" className="text-red-600">{t("offline.dashboardError")}</p>
          <Button variant="outline" onClick={reload}>{t("offline.retry")}</Button>
        </CardContent></Card>
      ) : docs.length === 0 ? (
        <Card><CardContent className="py-12 text-center text-gray-500 dark:text-gray-400">
          {t("upload.drop")}
        </CardContent></Card>
      ) : (
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest app/dashboard/__tests__/page.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/app/dashboard/page.tsx frontend/app/dashboard/__tests__/page.test.tsx
git commit -m "feat: distinguish offline/load-error from empty document list on the dashboard, add retry"
```

---

### Task 5: Disable capture/upload while offline in `UploadExperience`

**Files:**
- Modify: `frontend/components/UploadExperience.tsx`
- Test: `frontend/components/__tests__/UploadExperience.test.tsx` (new — check first whether one already exists)

Current entry points (`UploadExperience.tsx:139-153`):

```tsx
      <button
        onClick={() => setCameraOpen(true)}
        className="mt-2 flex w-full flex-col items-center justify-center gap-4 rounded-3xl bg-brand-gradient px-8 py-14 text-white shadow-soft transition-transform hover:scale-[1.02] active:scale-100"
        style={organization ? { background: "linear-gradient(135deg, var(--org-primary), var(--org-accent))" } : undefined}
      >
        <Camera className="h-20 w-20" />
        <span className="text-2xl font-bold">{t("upload.takePhoto")}</span>
      </button>

      {/* Secondary: upload an existing file / PDF. */}
      <button onClick={() => inputRef.current?.click()} className="text-base font-medium text-brand underline-offset-4 hover:underline">
        {t("upload.orChooseFile")}
      </button>
```

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/UploadExperience.test.tsx
import { render, screen, act } from "@testing-library/react";
import { UploadExperience } from "@/components/UploadExperience";
import { LanguageProvider } from "@/lib/language-context";
import { OrganizationProvider } from "@/lib/organization-context";

jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
jest.mock("@/lib/auth-context", () => ({ useAuth: () => ({ user: null, loading: false }) }));

function renderExperience() {
  window.localStorage.setItem("lang", "en");
  return render(
    <LanguageProvider>
      <OrganizationProvider><UploadExperience /></OrganizationProvider>
    </LanguageProvider>
  );
}

describe("UploadExperience offline handling", () => {
  afterEach(() => {
    Object.defineProperty(window.navigator, "onLine", { value: true, configurable: true });
  });

  it("disables the camera and file-choice entry points while offline", () => {
    renderExperience();
    act(() => window.dispatchEvent(new Event("offline")));

    expect(screen.getByText("Take a photo").closest("button")).toBeDisabled();
    expect(screen.getByText("Or choose a file").closest("button")).toBeDisabled();
    expect(screen.getByText(/need an internet connection/)).toBeInTheDocument();
  });

  it("re-enables them once back online", () => {
    renderExperience();
    act(() => window.dispatchEvent(new Event("offline")));
    act(() => window.dispatchEvent(new Event("online")));

    expect(screen.getByText("Take a photo").closest("button")).not.toBeDisabled();
  });
});
```

(Check the exact `upload.takePhoto`/`upload.orChooseFile` English strings in `dictionaries.ts` before finalizing `getByText` — this plan assumes "Take a photo" / "Or choose a file" based on the key names; adjust the test literals to whatever those keys actually resolve to.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/UploadExperience.test.tsx`
Expected: FAIL — neither button is ever disabled today.

- [ ] **Step 3: Implement**

Add the import:

```tsx
import { useOnlineStatus } from "@/lib/useOnlineStatus";
```

Add the hook call near the top of the component:

```tsx
  const online = useOnlineStatus();
```

Update the two entry points and add the explanatory line:

```tsx
      <button
        onClick={() => setCameraOpen(true)}
        disabled={!online}
        className="mt-2 flex w-full flex-col items-center justify-center gap-4 rounded-3xl bg-brand-gradient px-8 py-14 text-white shadow-soft transition-transform hover:scale-[1.02] active:scale-100 disabled:opacity-50 disabled:hover:scale-100"
        style={organization ? { background: "linear-gradient(135deg, var(--org-primary), var(--org-accent))" } : undefined}
      >
        <Camera className="h-20 w-20" />
        <span className="text-2xl font-bold">{t("upload.takePhoto")}</span>
      </button>

      {/* Secondary: upload an existing file / PDF. */}
      <button onClick={() => inputRef.current?.click()} disabled={!online} className="text-base font-medium text-brand underline-offset-4 hover:underline disabled:opacity-50 disabled:no-underline">
        {t("upload.orChooseFile")}
      </button>

      {!online && <p className="text-sm text-amber-700 dark:text-amber-400">{t("offline.uploadDisabled")}</p>}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/UploadExperience.test.tsx`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/components/UploadExperience.tsx frontend/components/__tests__/UploadExperience.test.tsx
git commit -m "feat: disable capture/upload while offline, with an explanatory line"
```

---

## Self-Review Notes

- **Spec coverage:** global offline banner via online/offline events, rendered in layout.tsx alongside other global singletons ✓ Task 3; dashboard distinguishes offline/error from empty, with retry ✓ Task 4; capture/upload disabled while offline with explanation ✓ Task 5.
- **Explicitly out of scope (per the ticket's own "Stretch:" label):** holding a failed upload in IndexedDB and offering "send when you're back online." Not implemented here — it's a materially bigger feature (background sync, conflict handling for a page reload mid-queue, IndexedDB schema) that the ticket itself marks as a stretch goal, not core scope. If wanted later, it would be its own plan.
- **Not touched:** `public/sw.js`. Its cache-then-network-fallback for navigations is precisely why the app shell still renders while offline (enabling this plan's UI-level messaging in the first place) — no service-worker change is needed to distinguish "offline" from "empty" at the page level.
