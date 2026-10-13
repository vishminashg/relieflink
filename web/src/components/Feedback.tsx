import type { ReactNode } from "react";
import type { ApiError } from "../api/client";

export function ErrorBanner({ error, onRetry }: { error: ApiError | null; onRetry?: () => void }) {
  if (!error) return null;
  const fieldMessages = Object.values(error.fields).flat();
  return (
    <div className="banner banner-error" role="alert">
      <div>
        <strong>{error.message}</strong>
        {fieldMessages.length > 0 && (
          <ul>{fieldMessages.map((m, i) => <li key={i}>{m}</li>)}</ul>
        )}
      </div>
      {onRetry && <button className="btn btn-quiet" onClick={onRetry}>Try again</button>}
    </div>
  );
}

export function Loading({ label = "Loading" }: { label?: string }) {
  return <p className="loading" role="status">{label}…</p>;
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="empty">
      <p className="empty-title">{title}</p>
      {children && <p>{children}</p>}
    </div>
  );
}
