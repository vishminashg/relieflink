import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";

/** Loads data when the dependencies change, and exposes loading, error and a reload function. */
export function useLoad<T>(load: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(true);
  const counter = useRef(0);

  const run = useCallback(() => {
    const mine = ++counter.current; // ignore answers that arrive after a newer request started
    setLoading(true);
    load()
      .then((result) => { if (mine === counter.current) { setData(result); setError(null); } })
      .catch((e) => { if (mine === counter.current) setError(e instanceof ApiError ? e : new ApiError(0, String(e))); })
      .finally(() => { if (mine === counter.current) setLoading(false); });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  useEffect(() => { run(); }, [run]);

  return { data, error, loading, reload: run, setData };
}
