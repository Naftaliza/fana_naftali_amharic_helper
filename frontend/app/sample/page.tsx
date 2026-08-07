import type { Metadata } from "next";
import { SampleView } from "@/components/SampleView";

// Server component so this route gets its own <meta> tags (title/description) distinct from
// the root layout's — a WhatsApp forward of /sample should read as "see an example" rather than
// the generic app title. Image inherits the root's app/opengraph-image.tsx.
export const metadata: Metadata = {
  title: "Fana — see an example",
  description: "See what a document analysis looks like on Fana — no upload needed.",
};

export default function SamplePage() {
  return <SampleView />;
}
