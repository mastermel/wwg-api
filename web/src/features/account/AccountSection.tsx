import type { ReactNode } from "react";
import { Section } from "@/components/Section";

/** One part of the account page: a Section, named for screen readers by its title. */
export function AccountSection({
  title,
  description,
  tone,
  children,
}: {
  title: string;
  description?: string;
  tone?: "danger";
  children: ReactNode;
}) {
  return (
    <Section title={title} description={description} tone={tone}>
      {children}
    </Section>
  );
}
