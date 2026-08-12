import { UploadExperience } from "@/components/UploadExperience";
import { HomeExplainer } from "@/components/HomeExplainer";

// /upload renders the same experience as the home page (kept for existing links), and now has
// the same nav entry point too (BottomNav's "New document" tab always routes here regardless of
// auth state) — so it needs the same below-the-fold trust/share content HomeExplainer adds on
// "/", or an anonymous visitor arriving via that tab silently loses it. HomeExplainer already
// hides itself for signed-in/tenant-branded visitors, so this is a no-op for them either way.
export default function UploadPage() {
  return (
    <>
      <UploadExperience />
      <HomeExplainer />
    </>
  );
}
