import { useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import type { WorkflowStatus } from "../api/types";
import { StatusPill } from "../components/Badges";
import { EmptyState, ErrorBanner, Loading } from "../components/Feedback";
import { Pager } from "../components/Pager";
import { formatDateTime } from "../lib/format";
import { useLoad } from "../lib/useLoad";
import { statusLabel } from "../lib/workflow";

const STATUSES: WorkflowStatus[] = [
  "PendingCoordinatorApproval", "Planning", "Validating", "Approved", "Executed", "RevisionRequested", "Rejected", "FailedSafe",
];

export function Workflows() {
  const [status, setStatus] = useState<WorkflowStatus | "">("PendingCoordinatorApproval");
  const [page, setPage] = useState(1);
  const { data, error, loading, reload } = useLoad(() => api.workflows({ status, page, pageSize: 15 }), [status, page]);

  return (
    <>
      <header className="page-head">
        <h1>AI plans</h1>
        <p className="muted">Plans drafted by the planning agent. Nothing is carried out until a coordinator approves it.</p>
      </header>

      <div className="filters">
        <label>Show
          <select value={status} onChange={(e) => { setStatus(e.target.value as WorkflowStatus | ""); setPage(1); }}>
            <option value="">All plans</option>
            {STATUSES.map((s) => <option key={s} value={s}>{statusLabel(s)}</option>)}
          </select>
        </label>
      </div>

      <ErrorBanner error={error} onRetry={reload} />

      {loading && !data ? <Loading /> : data && data.items.length === 0 ? (
        <EmptyState title="No plans here.">Open a submitted request and choose “Draft a plan with AI”.</EmptyState>
      ) : data && (
        <>
          <ul className="queue" aria-busy={loading}>
            {data.items.map((w) => (
              <li key={w.id}>
                <Link className="row row-plain" to={`/workflows/${w.id}`}>
                  <span className="row-main">
                    <span className="row-title">{w.objective}</span>
                    <span className="row-sub">
                      Started {formatDateTime(w.createdAt)}
                      {w.revisionCount > 0 && ` · revised ${w.revisionCount} ${w.revisionCount === 1 ? "time" : "times"}`}
                    </span>
                  </span>
                  <span className="row-side"><StatusPill status={w.status} /></span>
                </Link>
              </li>
            ))}
          </ul>
          <Pager page={data.page} totalPages={data.totalPages} onChange={setPage} />
        </>
      )}
    </>
  );
}
