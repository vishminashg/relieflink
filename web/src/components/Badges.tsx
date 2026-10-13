import type { Urgency } from "../api/types";
import { statusLabel } from "../lib/workflow";

export function UrgencyTag({ urgency, overridden }: { urgency: Urgency; overridden?: boolean }) {
  return (
    <span className={`tag urgency-${urgency.toLowerCase()}`}>
      {urgency}
      {overridden && <span className="tag-note" title="A coordinator set this by hand"> (set by hand)</span>}
    </span>
  );
}

export function StatusPill({ status }: { status: string }) {
  return <span className={`pill status-${status.toLowerCase()}`}>{statusLabel(status)}</span>;
}
