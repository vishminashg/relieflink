import type { WorkflowStatus } from "../api/types";
import { pathStates, statusLabel } from "../lib/workflow";

/** The route a workflow takes, with where it is now. This one really is a sequence, so the stages are numbered. */
export function WorkflowPath({ status }: { status: WorkflowStatus }) {
  const states = pathStates(status);
  const stopped = status === "FailedSafe" || status === "Rejected" || status === "RevisionRequested";
  return (
    <div>
      <ol className="path" aria-label="Workflow progress">
        {states.map((s, i) => (
          <li key={s.label} className={`path-step path-${s.state}`} aria-current={s.state === "current" ? "step" : undefined}>
            <span className="path-dot">{s.state === "done" ? "✓" : i + 1}</span>
            <span className="path-label">{s.label}</span>
          </li>
        ))}
      </ol>
      {stopped && (
        <p className={`path-stop ${status === "RevisionRequested" ? "" : "path-stop-end"}`}>
          {status === "FailedSafe" && "The workflow stopped safely. Nothing was sent to the field."}
          {status === "Rejected" && "The plan was rejected."}
          {status === "RevisionRequested" && `${statusLabel(status)}: the planner is working on a new plan.`}
        </p>
      )}
    </div>
  );
}
