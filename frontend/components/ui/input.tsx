import * as React from "react";
import { cn } from "@/lib/utils";

export const Input = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...props }, ref) => (
    <input
      ref={ref}
      className={cn(
        "flex h-11 w-full rounded-xl border border-gray-300 bg-white px-4 py-2 text-base",
        "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand placeholder:text-gray-400",
        className
      )}
      {...props}
    />
  )
);
Input.displayName = "Input";
