# Confirm Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all six `window.confirm()` call sites with a single accessible, translated, RTL-aware `ConfirmDialog` component, and surface the two silently-swallowed delete failures as visible errors.

**Architecture:** One new presentational component (`components/ConfirmDialog.tsx`) copying the modal shell already proven in `components/Onboarding.tsx` (role="dialog", aria-modal, dir-aware, backdrop, brand buttons), plus a tiny `useConfirm()`-style local-state pattern at each of the 6 call sites (open/pending-action state, no global store needed). New `confirm.*` i18n keys added to all three dictionaries. Two of the six sites (`dashboard/page.tsx`, `documents/[id]/page.tsx`) also get a visible error message on delete failure instead of an empty catch.

**Tech Stack:** Next.js 15 / React 19, Jest + @testing-library/react, existing `useLanguage()` context, existing `Button`/`Card` UI primitives.

---

## File Structure

- Create: `frontend/components/ConfirmDialog.tsx` — the dialog itself (controlled component: `open`, `title`, `body`, `confirmLabel`, `onConfirm`, `onCancel`, `destructive?: boolean`).
- Create: `frontend/components/__tests__/ConfirmDialog.test.tsx`
- Modify: `frontend/i18n/dictionaries.ts` — add `confirm.cancel` to all three language blocks (he/am/en), and rename the ad-hoc per-action confirm keys' *usage* (the strings themselves stay, they become the dialog body).
- Modify: `frontend/app/dashboard/page.tsx` — swap `window.confirm` for `ConfirmDialog`, surface delete failure.
- Modify: `frontend/app/documents/[id]/page.tsx` — swap `window.confirm` for `ConfirmDialog`.
- Modify: `frontend/app/profile/page.tsx` — swap `window.confirm` for `ConfirmDialog`.
- Modify: `frontend/app/admin/providers/page.tsx` — swap `window.confirm` for `ConfirmDialog` (`ManageTab`'s `remove`).
- Modify: `frontend/app/admin/providers/InvoiceCell.tsx` — swap `window.confirm` for `ConfirmDialog`.
- Modify: `frontend/app/admin/organizations/page.tsx` — swap `window.confirm` for `ConfirmDialog`.

---

### Task 1: Build `ConfirmDialog`

**Files:**
- Create: `frontend/components/ConfirmDialog.tsx`
- Test: `frontend/components/__tests__/ConfirmDialog.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/components/__tests__/ConfirmDialog.test.tsx
import { render, screen, fireEvent } from "@testing-library/react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { LanguageProvider } from "@/lib/language-context";

function renderDialog(props: Partial<React.ComponentProps<typeof ConfirmDialog>> = {}) {
  const onConfirm = jest.fn();
  const onCancel = jest.fn();
  render(
    <LanguageProvider>
      <ConfirmDialog
        open
        title="Delete this document?"
        body="This cannot be undone."
        confirmLabel="Delete document"
        onConfirm={onConfirm}
        onCancel={onCancel}
        {...props}
      />
    </LanguageProvider>
  );
  return { onConfirm, onCancel };
}

describe("ConfirmDialog", () => {
  it("renders nothing when closed", () => {
    render(
      <LanguageProvider>
        <ConfirmDialog
          open={false}
          title="x" body="y" confirmLabel="z"
          onConfirm={jest.fn()} onCancel={jest.fn()}
        />
      </LanguageProvider>
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("shows title, body, and a real verb on the confirm button — never OK/Cancel", () => {
    renderDialog();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Delete this document?")).toBeInTheDocument();
    expect(screen.getByText("This cannot be undone.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete document" })).toBeInTheDocument();
    expect(screen.queryByText("OK")).not.toBeInTheDocument();
  });

  it("calls onConfirm when the confirm button is clicked", () => {
    const { onConfirm } = renderDialog();
    fireEvent.click(screen.getByRole("button", { name: "Delete document" }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("calls onCancel when the cancel button is clicked", () => {
    const { onCancel } = renderDialog();
    fireEvent.click(screen.getByRole("button", { name: "confirm.cancel" }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it("is a labeled, modal dialog", () => {
    renderDialog();
    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAttribute("aria-label", "Delete this document?");
  });

  it("renders the destructive variant in red", () => {
    renderDialog({ destructive: true });
    expect(screen.getByRole("button", { name: "Delete document" }).className).toMatch(/red/);
  });
});
```

Note: `LanguageProvider`'s default language is Hebrew (`he`), so `t("confirm.cancel")` will resolve to the Hebrew string once Task 2 adds it — until then it falls back to the raw key `"confirm.cancel"` (see `language-context.tsx:44`, `dictionaries[language][key] ?? key`), which is why the cancel-button test above matches on the key itself: it must pass both before and after Task 2's key is added.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest components/__tests__/ConfirmDialog.test.tsx`
Expected: FAIL — `Cannot find module '@/components/ConfirmDialog'`

- [ ] **Step 3: Write the component**

```tsx
// frontend/components/ConfirmDialog.tsx
"use client";

import { useLanguage } from "@/lib/language-context";
import { Button } from "@/components/ui/button";

/**
 * Accessible replacement for window.confirm(): translated, RTL-aware, and — critically — the
 * confirm button always carries a real verb ("Delete document") instead of the browser/OS
 * locale's untranslated "OK", which a user whose device locale differs from their chosen
 * app language may not be able to read. Copies the modal shell already proven in Onboarding.tsx.
 */
export function ConfirmDialog({
  open,
  title,
  body,
  confirmLabel,
  destructive = false,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  body: string;
  confirmLabel: string;
  destructive?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { t, rtl } = useLanguage();

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
    >
      <div dir={rtl ? "rtl" : "ltr"} className="relative w-full max-w-sm rounded-3xl bg-white p-6 text-center shadow-soft dark:bg-gray-900">
        <h2 className="mb-2 text-xl font-bold">{title}</h2>
        <p className="mb-6 text-gray-600 dark:text-gray-400">{body}</p>
        <div className="flex gap-3">
          <Button variant="outline" className="flex-1" onClick={onCancel}>
            {t("confirm.cancel")}
          </Button>
          <Button
            className={destructive ? "flex-1 bg-red-600 hover:bg-red-700 dark:bg-red-700 dark:hover:bg-red-600" : "flex-1"}
            onClick={onConfirm}
          >
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest components/__tests__/ConfirmDialog.test.tsx`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/components/ConfirmDialog.tsx frontend/components/__tests__/ConfirmDialog.test.tsx
git commit -m "feat: add accessible ConfirmDialog to replace window.confirm"
```

---

### Task 2: Add `confirm.cancel` to all three dictionaries

**Files:**
- Modify: `frontend/i18n/dictionaries.ts`

- [ ] **Step 1: Add the key to the Hebrew block**

Add this line right after `"doc.confirmDelete"` (`dictionaries.ts:141`, inside the `he` block):

```ts
  "confirm.cancel": "ביטול",
```

- [ ] **Step 2: Add the key to the Amharic block**

Add right after `"doc.confirmDelete"` in the `am` block (`dictionaries.ts:456`):

```ts
  "confirm.cancel": "ይቅር",
```

- [ ] **Step 3: Add the key to the English block**

Add right after `"doc.confirmDelete"` in the `en` block (`dictionaries.ts:771`):

```ts
  "confirm.cancel": "Cancel",
```

- [ ] **Step 4: Run the full dictionary/type test and the dialog test**

Run: `cd frontend && npx jest lib/__tests__/types.test.ts components/__tests__/ConfirmDialog.test.tsx`
Expected: PASS — the `onCancel` test in Task 1 now matches on the real Hebrew string instead of falling back to the raw key. Update that one assertion:

```tsx
// in ConfirmDialog.test.tsx, replace:
fireEvent.click(screen.getByRole("button", { name: "confirm.cancel" }));
// with:
fireEvent.click(screen.getByRole("button", { name: "ביטול" }));
```

- [ ] **Step 5: Re-run and commit**

Run: `cd frontend && npx jest components/__tests__/ConfirmDialog.test.tsx`
Expected: PASS

```bash
git add frontend/i18n/dictionaries.ts frontend/components/__tests__/ConfirmDialog.test.tsx
git commit -m "feat: add confirm.cancel translation key (he/am/en)"
```

---

### Task 3: Wire into `dashboard/page.tsx` — delete confirm + visible failure

**Files:**
- Modify: `frontend/app/dashboard/page.tsx`
- Test: `frontend/app/dashboard/__tests__/page.test.tsx` (new)

Current code (`dashboard/page.tsx:22-34`):

```tsx
  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.preventDefault(); // don't navigate into the document
    if (!window.confirm(t("doc.confirmDelete"))) return;
    setDeletingId(id);
    try {
      await api.deleteDocument(id);
      setDocs((list) => list.filter((d) => d.id !== id));
    } catch {
      // leave the row in place if the delete failed
    } finally {
      setDeletingId(null);
    }
  };
```

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/app/dashboard/__tests__/page.test.tsx
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import DashboardPage from "@/app/dashboard/page";
import { LanguageProvider } from "@/lib/language-context";
import { AuthProvider } from "@/lib/auth-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { listDocuments: jest.fn(), deleteDocument: jest.fn() },
}));
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({ user: { id: "u1", email: "a@b.com" }, loading: false }),
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const DOC = { id: "d1", fileName: "letter.pdf", contentType: "application/pdf", uploadedAt: new Date().toISOString(), hasAnalysis: true, status: 2 };

function renderPage() {
  return render(
    <LanguageProvider>
      <AuthProvider><DashboardPage /></AuthProvider>
    </LanguageProvider>
  );
}

describe("DashboardPage delete flow", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (api.listDocuments as jest.Mock).mockResolvedValue([DOC]);
  });

  it("shows a ConfirmDialog instead of window.confirm before deleting", async () => {
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByLabelText("Delete document"));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.deleteDocument).not.toHaveBeenCalled();
  });

  it("deletes the document only after the dialog is confirmed", async () => {
    (api.deleteDocument as jest.Mock).mockResolvedValue(undefined);
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByLabelText("Delete document"));
    fireEvent.click(screen.getByRole("button", { name: "Delete document" }));

    await waitFor(() => expect(api.deleteDocument).toHaveBeenCalledWith("d1"));
    await waitFor(() => expect(screen.queryByText("letter.pdf")).not.toBeInTheDocument());
  });

  it("shows a visible error and keeps the row when delete fails", async () => {
    (api.deleteDocument as jest.Mock).mockRejectedValue(new Error("boom"));
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByLabelText("Delete document"));
    fireEvent.click(screen.getByRole("button", { name: "Delete document" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByText("letter.pdf")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest app/dashboard/__tests__/page.test.tsx`
Expected: FAIL — clicking delete calls `window.confirm` synchronously (jsdom auto-confirms `false` by default, so nothing happens and no dialog role exists).

- [ ] **Step 3: Implement**

Replace lines 1-42 of `dashboard/page.tsx`:

```tsx
"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { FileText, Plus, Trash2 } from "lucide-react";
import { api } from "@/lib/api";
import { useLanguage } from "@/lib/language-context";
import { useAuth } from "@/lib/auth-context";
import { useRouter } from "next/navigation";
import { DOCUMENT_STATUS, type DocumentSummary } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { ConfirmDialog } from "@/components/ConfirmDialog";

export default function DashboardPage() {
  const { t } = useLanguage();
  const { user, loading } = useAuth();
  const router = useRouter();
  const [docs, setDocs] = useState<DocumentSummary[]>([]);
  const [fetching, setFetching] = useState(true);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const requestDelete = (id: string, e: React.MouseEvent) => {
    e.preventDefault(); // don't navigate into the document
    setDeleteError(null);
    setConfirmId(id);
  };

  const confirmDelete = async () => {
    const id = confirmId!;
    setConfirmId(null);
    setDeletingId(id);
    try {
      await api.deleteDocument(id);
      setDocs((list) => list.filter((d) => d.id !== id));
    } catch {
      setDeleteError(t("doc.deleteError"));
    } finally {
      setDeletingId(null);
    }
  };
```

Then update the delete button (was `dashboard/page.tsx:80-88`):

```tsx
                <button
                  onClick={(e) => requestDelete(d.id, e)}
                  disabled={deletingId === d.id}
                  aria-label={t("doc.delete")}
                  title={t("doc.delete")}
                  className="shrink-0 rounded-lg p-2 text-gray-400 transition-colors hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40 disabled:opacity-50"
                >
                  <Trash2 className="h-5 w-5" />
                </button>
```

(unchanged except the handler name), and add the error message + dialog just before the closing `</div>` of the component's root:

```tsx
      {deleteError && <p role="alert" className="text-sm text-red-600">{deleteError}</p>}

      <ConfirmDialog
        open={confirmId !== null}
        title={t("doc.delete")}
        body={t("doc.confirmDelete")}
        confirmLabel={t("doc.delete")}
        destructive
        onConfirm={confirmDelete}
        onCancel={() => setConfirmId(null)}
      />
    </div>
  );
}
```

- [ ] **Step 4: Add the `doc.deleteError` key to all three dictionaries**

Hebrew (after `doc.confirmDelete`, `dictionaries.ts:141`):
```ts
  "doc.deleteError": "המחיקה נכשלה. נסו שוב.",
```
Amharic (after `doc.confirmDelete`, `dictionaries.ts:456`):
```ts
  "doc.deleteError": "መሰረዝ አልተሳካም። እንደገና ይሞክሩ።",
```
English (after `doc.confirmDelete`, `dictionaries.ts:771`):
```ts
  "doc.deleteError": "Delete failed. Please try again.",
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd frontend && npx jest app/dashboard/__tests__/page.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 6: Commit**

```bash
git add frontend/app/dashboard/page.tsx frontend/app/dashboard/__tests__/page.test.tsx frontend/i18n/dictionaries.ts
git commit -m "feat: replace window.confirm with ConfirmDialog on dashboard, surface delete failures"
```

---

### Task 4: Wire into `documents/[id]/page.tsx` — cancel confirm

**Files:**
- Modify: `frontend/app/documents/[id]/page.tsx`
- Test: `frontend/app/documents/[id]/__tests__/page.test.tsx` (new — create if it doesn't already exist; check first with `ls frontend/app/documents/[id]/__tests__/` since this plan doesn't know if one exists)

Current code (`documents/[id]/page.tsx:37-46`):

```tsx
  const cancel = async () => {
    if (!window.confirm(t("doc.confirmDelete"))) return;
    setCancelling(true);
    try {
      await api.deleteDocument(id);
      router.push("/dashboard");
    } catch {
      setCancelling(false);
    }
  };
```

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/app/documents/[id]/__tests__/page.test.tsx
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import DocumentDetailPage from "@/app/documents/[id]/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({
  api: { getDocument: jest.fn(), deleteDocument: jest.fn(), analyze: jest.fn() },
}));
jest.mock("next/navigation", () => ({
  useParams: () => ({ id: "d1" }),
  useRouter: () => ({ push: jest.fn() }),
}));

const PENDING_DOC = {
  id: "d1", fileName: "letter.pdf", contentType: "application/pdf", ocrText: null,
  uploadedAt: new Date().toISOString(), analysis: null, status: 0,
  processedPages: 0, totalPages: 1, skippedPages: 0, processingError: null,
};

function renderPage() {
  return render(<LanguageProvider><DocumentDetailPage /></LanguageProvider>);
}

describe("DocumentDetailPage cancel flow", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (api.getDocument as jest.Mock).mockResolvedValue(PENDING_DOC);
  });

  it("shows a ConfirmDialog instead of window.confirm before cancelling", async () => {
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByRole("button", { name: /Cancel/ }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.deleteDocument).not.toHaveBeenCalled();
  });

  it("deletes and navigates away only once confirmed", async () => {
    (api.deleteDocument as jest.Mock).mockResolvedValue(undefined);
    renderPage();
    await waitFor(() => screen.getByText("letter.pdf"));

    fireEvent.click(screen.getByRole("button", { name: /Cancel/ }));
    fireEvent.click(screen.getByRole("button", { name: "Delete document" }));

    await waitFor(() => expect(api.deleteDocument).toHaveBeenCalledWith("d1"));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest "app/documents/[id]/__tests__/page.test.tsx"`
Expected: FAIL — no dialog role appears on click.

- [ ] **Step 3: Implement**

Add the import:

```tsx
import { ConfirmDialog } from "@/components/ConfirmDialog";
```

Replace the `cancel` function and add a `confirmingCancel` state:

```tsx
  const [confirmingCancel, setConfirmingCancel] = useState(false);

  const cancel = async () => {
    setCancelling(true);
    try {
      await api.deleteDocument(id);
      router.push("/dashboard");
    } catch {
      setCancelling(false);
    }
  };
```

Update the cancel button (was `documents/[id]/page.tsx:121-123`) to open the dialog instead of calling `cancel` directly:

```tsx
          <Button variant="outline" onClick={() => setConfirmingCancel(true)} disabled={cancelling}>
            <X className="h-5 w-5" />{t("doc.cancel")}
          </Button>
```

Add the dialog just before the component's closing `</div>`:

```tsx
      <ConfirmDialog
        open={confirmingCancel}
        title={t("doc.delete")}
        body={t("doc.confirmDelete")}
        confirmLabel={t("doc.delete")}
        destructive
        onConfirm={() => { setConfirmingCancel(false); cancel(); }}
        onCancel={() => setConfirmingCancel(false)}
      />
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest "app/documents/[id]/__tests__/page.test.tsx"`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add "frontend/app/documents/[id]/page.tsx" "frontend/app/documents/[id]/__tests__/page.test.tsx"
git commit -m "feat: replace window.confirm with ConfirmDialog on document cancel"
```

---

### Task 5: Wire into `profile/page.tsx` — account deletion confirm

**Files:**
- Modify: `frontend/app/profile/page.tsx`
- Test: `frontend/app/profile/__tests__/page.test.tsx` (new)

Current code (`profile/page.tsx:47-59`):

```tsx
  const deleteAccount = async () => {
    if (!window.confirm(t("profile.deleteAccountConfirm"))) return;
    setError(null);
    setDeleting(true);
    try {
      await api.deleteAccount();
      logout();
      router.push("/");
    } catch {
      setError(t("profile.deleteError"));
      setDeleting(false);
    }
  };
```

- [ ] **Step 1: Write the failing test**

```tsx
// frontend/app/profile/__tests__/page.test.tsx
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import ProfilePage from "@/app/profile/page";
import { LanguageProvider } from "@/lib/language-context";
import { api } from "@/lib/api";

jest.mock("@/lib/api", () => ({ api: { deleteAccount: jest.fn(), exportAccount: jest.fn() } }));
jest.mock("next/navigation", () => ({ useRouter: () => ({ push: jest.fn() }) }));
const mockLogout = jest.fn();
jest.mock("@/lib/auth-context", () => ({
  useAuth: () => ({
    user: { id: "u1", email: "a@b.com", displayName: "A", preferredLanguage: 0 },
    loading: false, logout: mockLogout,
  }),
}));

function renderPage() {
  return render(<LanguageProvider><ProfilePage /></LanguageProvider>);
}

describe("ProfilePage delete account flow", () => {
  beforeEach(() => jest.clearAllMocks());

  it("shows a ConfirmDialog instead of window.confirm", () => {
    renderPage();
    fireEvent.click(screen.getByText("Delete account"));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(api.deleteAccount).not.toHaveBeenCalled();
  });

  it("deletes the account only once confirmed", async () => {
    (api.deleteAccount as jest.Mock).mockResolvedValue(undefined);
    renderPage();
    fireEvent.click(screen.getByText("Delete account"));
    fireEvent.click(screen.getByRole("button", { name: "Delete account" }));
    await waitFor(() => expect(api.deleteAccount).toHaveBeenCalled());
    expect(mockLogout).toHaveBeenCalled();
  });
});
```

(This test assumes English dictionary strings — `profile.deleteAccount` = "Delete account" per `dictionaries.ts:? ` (`en` block). Since `LanguageProvider` defaults to Hebrew, add `window.localStorage.setItem("lang", "en")` in a `beforeEach` if the real string differs; check the actual `en` value for `profile.deleteAccount` before finalizing the test name match.)

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx jest app/profile/__tests__/page.test.tsx`
Expected: FAIL — no dialog appears.

- [ ] **Step 3: Implement**

Add the import and a `confirming` state, replace `deleteAccount`:

```tsx
import { ConfirmDialog } from "@/components/ConfirmDialog";
// ...
  const [confirming, setConfirming] = useState(false);

  const deleteAccount = async () => {
    setError(null);
    setDeleting(true);
    try {
      await api.deleteAccount();
      logout();
      router.push("/");
    } catch {
      setError(t("profile.deleteError"));
      setDeleting(false);
    }
  };
```

Change the delete button's `onClick` (was `profile/page.tsx:78-85`) from `deleteAccount` to `() => setConfirming(true)`, and add the dialog before the component's final closing `</div>`:

```tsx
      <ConfirmDialog
        open={confirming}
        title={t("profile.deleteAccount")}
        body={t("profile.deleteAccountConfirm")}
        confirmLabel={t("profile.deleteAccount")}
        destructive
        onConfirm={() => { setConfirming(false); deleteAccount(); }}
        onCancel={() => setConfirming(false)}
      />
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx jest app/profile/__tests__/page.test.tsx`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add frontend/app/profile/page.tsx frontend/app/profile/__tests__/page.test.tsx
git commit -m "feat: replace window.confirm with ConfirmDialog on account deletion"
```

---

### Task 6: Wire into admin pages — providers list, invoice generation, organization deactivate

**Files:**
- Modify: `frontend/app/admin/providers/page.tsx` (the `ManageTab.remove` function, ~line 139-144)
- Modify: `frontend/app/admin/providers/InvoiceCell.tsx` (the `generate` function, ~line 42-54)
- Modify: `frontend/app/admin/organizations/page.tsx` (the `toggleStatus` function, ~line 60-70)
- Test: `frontend/app/admin/providers/__tests__/InvoiceCell.test.tsx` (already exists — extend it)

This is the ticket's incidental finding: `admin/providers/page.tsx:140` reuses `doc.confirmDelete` for deleting a *provider*, which is semantically wrong (it says "document"). Fix the copy-paste bug at the same time as the mechanical swap by giving it its own key.

- [ ] **Step 1: Add a provider-specific confirm key to all three dictionaries**

Hebrew (near `doc.confirmDelete`, `dictionaries.ts:141`):
```ts
  "admin.provider.confirmDelete": "להסיר את נותן השירות הזה לצמיתות?",
```
Amharic:
```ts
  "admin.provider.confirmDelete": "ይህን የአገልግሎት ሰጪ ለዘላለም ማስወገድ ይፈልጋሉ?",
```
English:
```ts
  "admin.provider.confirmDelete": "Permanently remove this provider?",
```

- [ ] **Step 2: Read the existing InvoiceCell test to match its render helper**

Run: `cat "frontend/app/admin/providers/__tests__/InvoiceCell.test.tsx"` and reuse whatever `render(...)` wrapper it already uses (likely wraps in `LanguageProvider`).

- [ ] **Step 3: Add a failing test to InvoiceCell.test.tsx**

Add this test case to the existing file (adjust the render helper name to match what's already there):

```tsx
  it("shows a ConfirmDialog instead of window.confirm before generating", () => {
    renderCell(); // use the file's existing render helper
    fireEvent.click(screen.getByText("Generate invoice")); // match the file's existing button text
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
```

- [ ] **Step 4: Run to verify it fails**

Run: `cd frontend && npx jest app/admin/providers/__tests__/InvoiceCell.test.tsx`
Expected: FAIL — no dialog role.

- [ ] **Step 5: Implement all three admin sites**

`InvoiceCell.tsx` — add `import { ConfirmDialog } from "@/components/ConfirmDialog";`, a `confirming` state, split `generate` so the confirm only gates the call:

```tsx
  const [confirming, setConfirming] = useState(false);

  const generate = async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await api.adminGenerateInvoice(providerId, year, month);
      setInvoice(created);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };
```
Change whatever button currently calls `generate` directly to instead call `() => setConfirming(true)`, and render:
```tsx
      <ConfirmDialog
        open={confirming}
        title={t("invoice.generate")}
        body={t("invoice.confirm")}
        confirmLabel={t("invoice.generate")}
        onConfirm={() => { setConfirming(false); generate(); }}
        onCancel={() => setConfirming(false)}
      />
```
(Use whichever existing key names the button already renders for its label — inspect the file's JSX above line 42 for the exact label key, since this plan only read lines 30-54.)

`admin/providers/page.tsx` (`ManageTab`) — same pattern: add `confirmId` state, change `remove`'s body to run unconditionally, gate the button's `onClick` behind `setConfirmId(p.id)`, and use `t("admin.provider.confirmDelete")` as the body with `destructive`.

`admin/organizations/page.tsx` — same pattern for `toggleStatus`, but only when `next === false` (deactivating) should the dialog appear at all — when reactivating (`next === true`), call `api.adminSetOrganizationStatus` directly with no dialog, preserving the existing asymmetric behavior:

```tsx
  const [confirmDeactivateId, setConfirmDeactivateId] = useState<string | null>(null);

  const setActive = async (o: OrganizationSummary, next: boolean) => {
    setStatusBusyId(o.id);
    try {
      await api.adminSetOrganizationStatus(o.id, next);
      await reload();
    } finally {
      setStatusBusyId(null);
    }
  };

  const toggleStatus = (o: OrganizationSummary) => {
    const next = !o.isActive;
    if (next === false) { setConfirmDeactivateId(o.id); return; }
    setActive(o, next);
  };
```
Render the dialog once, keyed by `confirmDeactivateId`, resolving the target org from `orgs` before confirming.

- [ ] **Step 6: Run all three tests to verify they pass**

Run: `cd frontend && npx jest app/admin`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add frontend/app/admin frontend/i18n/dictionaries.ts
git commit -m "feat: replace window.confirm with ConfirmDialog across admin pages, fix provider-delete copy bug"
```

---

## Self-Review Notes

- **Spec coverage:** ConfirmDialog component with role/aria-modal/dir ✓ (Task 1); destructive red variant ✓ (Task 1); real verb on confirm button, never "OK"/"Cancel" ✓ (all tasks pass an explicit `confirmLabel`); all six call sites swapped ✓ (Tasks 3-6); `confirm.*` i18n keys added to all three dictionaries ✓ (Task 2); dashboard delete failure surfaced ✓ (Task 3); documents-page cancel failure was already surfaced (`setCancelling(false)` leaves the cancel button re-enabled, no silent success) — no change needed there beyond the dialog swap; provider-delete copy bug fixed as a bonus ✓ (Task 6).
- **Not in scope for this plan:** focus-trap / Escape-to-close on `ConfirmDialog` — `Onboarding.tsx`'s dialog (the pattern this copies) doesn't have it either, so this plan keeps parity rather than unilaterally adding new a11y behavior beyond the ticket's ask. If wanted later, `CameraCapture.tsx`'s `useEffect` (Escape + focus-restore, lines 169-178) is the pattern to copy.
