import { UploadExperience } from "@/components/UploadExperience";
import { HomeExplainer } from "@/components/HomeExplainer";

// The home page IS the upload experience — no separate marketing landing page. HomeExplainer
// adds below-the-fold trust signals/share/help links without competing with the one-tap camera
// button for attention; it hides itself for signed-in and tenant-branded visitors.
export default function HomePage() {
  return (
    <>
      <UploadExperience />
      <HomeExplainer />
    </>
  );
}
