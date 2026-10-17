import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api, ApiError } from "../api/client";
import type { VolunteerAgentMatchResponse, VolunteerMatchResult, VolunteerSkillType } from "../api/types";
import { EmptyState, ErrorBanner, Loading } from "../components/Feedback";
import { DISTRICTS, formatDateTime, qualityClass, VOLUNTEER_SKILLS, skillLabel } from "../lib/format";

export function VolunteerMatch() {
  const [searchParams] = useSearchParams();

  // Initial parameters from query string
  const initialDistrict = searchParams.get("district") ?? "";
  const initialRequestId = searchParams.get("requestId") ?? "";
  const initialSkills = searchParams.get("skills")?.split(",").filter(Boolean) as VolunteerSkillType[] ?? [];

  // Default times: Start in 1 hour, end in 5 hours
  const now = new Date();
  const defaultStart = new Date(now.getTime() + 60 * 60 * 1000);
  const defaultEnd = new Date(defaultStart.getTime() + 4 * 60 * 60 * 1000);

  const toInputFormat = (d: Date) => {
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  };

  const [district, setDistrict] = useState(initialDistrict);
  const [selectedSkills, setSelectedSkills] = useState<VolunteerSkillType[]>(initialSkills);
  const [startTime, setStartTime] = useState(toInputFormat(defaultStart));
  const [endTime, setEndTime] = useState(toInputFormat(defaultEnd));
  const [requireAllSkills, setRequireAllSkills] = useState(false);
  const [limit, setLimit] = useState(10);

  const [reliefRequestId, setReliefRequestId] = useState(initialRequestId);

  // Results state
  const [results, setResults] = useState<VolunteerMatchResult[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [agentProposal, setAgentProposal] = useState<VolunteerAgentMatchResponse | null>(null);
  const [agentLoading, setAgentLoading] = useState(false);
  const [agentError, setAgentError] = useState<ApiError | null>(null);

  // Dispatch modal state
  const [dispatchCandidate, setDispatchCandidate] = useState<VolunteerMatchResult | null>(null);
  const [roleOrTask, setRoleOrTask] = useState("");
  const [notes, setNotes] = useState("");
  const [dispatching, setDispatching] = useState(false);
  const [dispatchError, setDispatchError] = useState<ApiError | null>(null);
  const [dispatchSuccess, setDispatchSuccess] = useState<string | null>(null);

  const toggleSkill = (skill: VolunteerSkillType) => {
    setSelectedSkills((prev) =>
      prev.includes(skill) ? prev.filter((s) => s !== skill) : [...prev, skill]
    );
  };

  const handleMatch = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const startUtc = new Date(startTime).toISOString();
      const endUtc = new Date(endTime).toISOString();

      const res = await api.matchVolunteers({
        district: district || undefined,
        skills: selectedSkills.length > 0 ? selectedSkills.join(",") : undefined,
        startTime: startUtc,
        endTime: endUtc,
        requireAllSkills,
        limit,
      });
      setResults(res);
    } catch (err) {
      setError(err instanceof ApiError ? err : new ApiError(0, "The matching request failed."));
    } finally {
      setLoading(false);
    }
  };

  const handleOpenDispatch = (candidate: VolunteerMatchResult) => {
    setDispatchCandidate(candidate);
    setDispatchError(null);
    setRoleOrTask(
      candidate.matchedSkills.length > 0
        ? `${candidate.matchedSkills.map(skillLabel).join(" & ")} Field Response`
        : "Emergency Relief Assistance"
    );
    setNotes(
      reliefRequestId ? `Linked to Relief Request ${reliefRequestId}` : ""
    );
  };

  const handleAgentMatch = async () => {
    if (!reliefRequestId.trim()) {
      setAgentError(new ApiError(400, "Link a relief request before asking the matching agent for a proposal."));
      return;
    }

    setAgentLoading(true);
    setAgentError(null);
    try {
      const response = await api.agentMatchVolunteers({
        requestId: reliefRequestId.trim(),
        district: district || null,
        requiredSkills: selectedSkills,
        startTime: new Date(startTime).toISOString(),
        endTime: new Date(endTime).toISOString(),
        volunteersNeeded: 1,
      });
      setAgentProposal(response);
    } catch (err) {
      setAgentError(err instanceof ApiError ? err : new ApiError(0, "The matching agent request failed."));
    } finally {
      setAgentLoading(false);
    }
  };

  const handleConfirmDispatch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!dispatchCandidate) return;
    setDispatching(true);
    setDispatchError(null);
    try {
      const startUtc = new Date(startTime).toISOString();
      const endUtc = new Date(endTime).toISOString();

      await api.createAssignment({
        volunteerId: dispatchCandidate.volunteer.id,
        reliefRequestId: reliefRequestId.trim() || null,
        startTime: startUtc,
        endTime: endUtc,
        roleOrTask: roleOrTask.trim() || null,
        notes: notes.trim() || null,
      });

      setDispatchSuccess(
        `Successfully dispatched ${dispatchCandidate.volunteer.user?.fullName ?? "Volunteer"} to mission without scheduling conflicts!`
      );
      setDispatchCandidate(null);
      // Re-run match to reflect updated schedule
      await handleMatch();
    } catch (err) {
      setDispatchError(err instanceof ApiError ? err : new ApiError(0, "The dispatch request failed."));
    } finally {
      setDispatching(false);
    }
  };

  return (
    <>
      <header className="page-head">
        <div className="page-head-row">
          <div>
            <h1>Volunteer Match &amp; Dispatch</h1>
            <p className="muted">
              Deterministic matching engine scoring district proximity, verified skills, and schedule availability with strict double-booking protection.
            </p>
          </div>
          <div>
            <Link className="btn btn-quiet" to="/volunteers">
              &larr; Volunteer Directory
            </Link>
          </div>
        </div>
      </header>

      {dispatchSuccess && (
        <div className="banner" style={{ background: "#dcefdc", border: "1px solid #a5cfa5", color: "#1d4a1d" }}>
          <div>
            <strong>Dispatch Confirmed:</strong> {dispatchSuccess}
          </div>
          <button
            className="btn btn-quiet"
            style={{ padding: "0.2rem 0.6rem", minHeight: "auto" }}
            onClick={() => setDispatchSuccess(null)}
            type="button"
          >
            &times;
          </button>
        </div>
      )}

      {/* Matching Form */}
      <form className="panel" onSubmit={handleMatch} aria-label="Volunteer Matching Criteria">
        <h2>Mission Deployment Criteria</h2>

        <div className="form-row stacked">
          <label className="grow">
            Target Disaster District
            <select
              value={district}
              onChange={(e) => setDistrict(e.target.value)}
            >
              <option value="">All districts (Nationwide search)</option>
              {DISTRICTS.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </label>

          <label className="grow">
            Linked Relief Request ID (Optional)
            <input
              type="text"
              value={reliefRequestId}
              onChange={(e) => setReliefRequestId(e.target.value)}
              placeholder="e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6"
            />
          </label>
        </div>

        <div className="form-row stacked">
          <label className="grow">
            Mission Start Time
            <input
              type="datetime-local"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              required
            />
          </label>

          <label className="grow">
            Mission End Time
            <input
              type="datetime-local"
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
              required
            />
          </label>

          <label style={{ minWidth: "6rem" }}>
            Max Candidates
            <select
              value={limit}
              onChange={(e) => setLimit(Number(e.target.value))}
            >
              <option value={5}>Top 5</option>
              <option value={10}>Top 10</option>
              <option value={20}>Top 20</option>
            </select>
          </label>
        </div>

        <div className="stacked">
          <label>Required Operational Skills</label>
          <div className="chips" style={{ marginTop: "0.5rem" }}>
            {VOLUNTEER_SKILLS.map((skill) => {
              const active = selectedSkills.includes(skill);
              return (
                <button
                  key={skill}
                  type="button"
                  onClick={() => toggleSkill(skill)}
                  className={`chip ${active ? "btn-primary" : ""}`}
                  style={{
                    cursor: "pointer",
                    padding: "0.3rem 0.8rem",
                    border: active ? "2px solid var(--signal-dark)" : "1px solid var(--line)",
                    background: active ? "var(--signal)" : "#e7eef1",
                    color: active ? "#fff" : "inherit",
                    fontWeight: active ? 700 : 400,
                  }}
                >
                  {skillLabel(skill)} {active ? "✓" : "+"}
                </button>
              );
            })}
          </div>
        </div>

        <div className="check-inline stacked">
          <input
            type="checkbox"
            id="requireAll"
            checked={requireAllSkills}
            onChange={(e) => setRequireAllSkills(e.target.checked)}
          />
          <label htmlFor="requireAll" style={{ fontWeight: 400 }}>
            Strict Match: Require candidate to possess <strong>all</strong> selected skills
          </label>
        </div>

        <div className="button-row">
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {loading ? "Evaluating Match Algorithms..." : "Find Matching Volunteers"}
          </button>
          <button className="btn btn-quiet" type="button" onClick={handleAgentMatch} disabled={agentLoading}>
            {agentLoading ? "Asking Matching Agent..." : "Ask Matching Agent"}
          </button>
          {results && (
            <button
              className="btn btn-quiet"
              type="button"
              onClick={() => {
                setResults(null);
                setError(null);
              }}
            >
              Clear Results
            </button>
          )}
        </div>
      </form>

      <ErrorBanner error={error} onRetry={handleMatch} />
      <ErrorBanner error={agentError} />

      {agentProposal && (
        <section className="panel" aria-labelledby="agent-proposal-heading">
          <div className="panel-head">
            <h2 id="agent-proposal-heading">Matching Agent Proposal</h2>
            <span className="muted">Backend safety checks remain authoritative</span>
          </div>
          <p>{agentProposal.proposal.summary}</p>
          <ul className="plain-list">
            {agentProposal.proposal.rationale.map((reason, index) => <li key={index}>{reason}</li>)}
          </ul>
          <div className="chips">
            {agentProposal.proposal.volunteerIds.map((id) => <span className="chip" key={id}>{id}</span>)}
          </div>
          <div className="chips" style={{ marginTop: "0.6rem" }}>
            {agentProposal.checks.map((check) => (
              <span className="chip" key={check.name} style={{ background: check.passed ? "#dcefdc" : "#fee2e2" }}>
                {check.passed ? "Pass" : "Review"}: {check.name}
              </span>
            ))}
          </div>
        </section>
      )}

      {/* Results Section */}
      {loading && !results && <Loading />}

      {results && results.length === 0 && (
        <EmptyState title="No eligible volunteers match these criteria.">
          No volunteers were found free of schedule conflict in or adjacent to the target district. Try broadening skills or adjusting the time window.
        </EmptyState>
      )}

      {results && results.length > 0 && (
        <section aria-labelledby="results-heading">
          <div className="panel-head">
            <h2 id="results-heading">
              Ranked Match Results ({results.length} eligible candidates)
            </h2>
            <span className="muted">Sorted by compatibility score (0 - 100)</span>
          </div>

          <div className="stack">
            {results.map((res, index) => (
              <article key={res.volunteer.id} className="panel match-card">
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "flex-start",
                    flexWrap: "wrap",
                    gap: "1rem",
                  }}
                >
                  <div>
                    <div style={{ display: "flex", alignItems: "center", gap: "0.6rem" }}>
                      <span className="muted" style={{ fontWeight: 700 }}>#{index + 1}</span>
                      <h3 style={{ margin: 0, fontSize: "1.2rem" }}>
                        <Link to={`/volunteers/${res.volunteer.id}`}>
                          {res.volunteer.user?.fullName ?? "Volunteer Candidate"}
                        </Link>
                      </h3>
                      <span className={`quality-tag ${qualityClass(res.quality)}`}>
                        {res.quality} ({res.score} / 100)
                      </span>
                      {res.volunteer.district === district && (
                        <span className="tag" style={{ background: "#e0f2fe", color: "#0369a1" }}>
                          Local District
                        </span>
                      )}
                    </div>

                    <p className="muted" style={{ margin: "0.3rem 0" }}>
                      Base: <strong>{res.volunteer.district}</strong> &bull; Contact: {res.volunteer.user?.phone ?? res.volunteer.emergencyContactPhone} &bull; Max: {res.volunteer.maxHoursPerWeek}h/wk
                    </p>

                    {/* Proximity & Availability indicators */}
                    <div style={{ display: "flex", gap: "0.8rem", flexWrap: "wrap", margin: "0.4rem 0" }}>
                      {res.hasScheduleConflict ? (
                        <span className="tag urgency-critical">Schedule Conflict Warning</span>
                      ) : (
                        <span className="available-badge">Zero Schedule Conflicts ✓</span>
                      )}

                      {res.hasAvailability ? (
                        <span className="tag" style={{ background: "#e8f5e9", color: "#2e7d32" }}>
                          Slot Explicitly Available
                        </span>
                      ) : (
                        <span className="tag" style={{ background: "#fff6e0", color: "#8a5300" }}>
                          Standard Standby
                        </span>
                      )}
                    </div>

                    {/* Matched & Missing Skills */}
                    <div style={{ marginTop: "0.6rem" }}>
                      <div className="chips">
                        {res.matchedSkills.map((s) => (
                          <span
                            key={s}
                            className="chip"
                            style={{ background: "#dcefdc", color: "#1d4a1d", fontWeight: 600 }}
                          >
                            ✓ {skillLabel(s)}
                          </span>
                        ))}
                        {res.missingSkills.map((s) => (
                          <span
                            key={s}
                            className="chip"
                            style={{ background: "#fee2e2", color: "#991b1b" }}
                          >
                            &times; Missing {skillLabel(s)}
                          </span>
                        ))}
                      </div>
                    </div>

                    {/* Reasons breakdown */}
                    <details style={{ marginTop: "0.6rem" }}>
                      <summary style={{ cursor: "pointer", color: "var(--signal-dark)", fontSize: "0.9rem" }}>
                        View Scoring Rationale ({res.reasons.length} factors)
                      </summary>
                      <ul className="plain-list" style={{ marginTop: "0.4rem", fontSize: "0.88rem" }}>
                        {res.reasons.map((r, i) => (
                          <li key={i} className="muted">{r}</li>
                        ))}
                      </ul>
                    </details>
                  </div>

                  <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", minWidth: "10rem" }}>
                    <button
                      className="btn btn-primary"
                      onClick={() => handleOpenDispatch(res)}
                      type="button"
                    >
                      Dispatch Volunteer
                    </button>
                    <Link
                      className="btn btn-quiet"
                      to={`/volunteers/${res.volunteer.id}`}
                    >
                      View Full Profile
                    </Link>
                  </div>
                </div>
              </article>
            ))}
          </div>
        </section>
      )}

      {/* Dispatch Modal */}
      {dispatchCandidate && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            background: "rgba(18, 38, 58, 0.6)",
            display: "grid",
            placeItems: "center",
            padding: "1rem",
            zIndex: 100,
          }}
        >
          <div className="login-card" style={{ maxWidth: "34rem" }}>
            <h2>Confirm Mission Dispatch</h2>
            <p className="muted">
              Assign <strong>{dispatchCandidate.volunteer.user?.fullName}</strong> to field duty. Schedule conflicts will be validated against active deployments with a 30-minute transit buffer.
            </p>

            <ErrorBanner error={dispatchError} />

            <form onSubmit={handleConfirmDispatch}>
              <div className="stacked">
                <label>
                  Mission Role / Task Description
                  <input
                    type="text"
                    value={roleOrTask}
                    onChange={(e) => setRoleOrTask(e.target.value)}
                    required
                  />
                </label>
              </div>

              <div className="stacked">
                <label>
                  Linked Relief Request ID (Optional)
                  <input
                    type="text"
                    value={reliefRequestId}
                    onChange={(e) => setReliefRequestId(e.target.value)}
                    placeholder="Leave empty for area patrol / general deployment"
                  />
                </label>
              </div>

              <div className="form-row stacked">
                <label className="grow">
                  Deployment Start (UTC)
                  <input
                    type="text"
                    value={formatDateTime(new Date(startTime).toISOString())}
                    disabled
                  />
                </label>
                <label className="grow">
                  Deployment End (UTC)
                  <input
                    type="text"
                    value={formatDateTime(new Date(endTime).toISOString())}
                    disabled
                  />
                </label>
              </div>

              <div className="stacked">
                <label>
                  Coordinator Dispatch Notes
                  <textarea
                    rows={2}
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    placeholder="Rendezvous point, radio channel, or staging area instructions..."
                  />
                </label>
              </div>

              <div className="button-row">
                <button
                  className="btn btn-primary"
                  type="submit"
                  disabled={dispatching}
                >
                  {dispatching ? "Validating & Assigning..." : "Confirm & Dispatch"}
                </button>
                <button
                  className="btn btn-quiet"
                  type="button"
                  onClick={() => setDispatchCandidate(null)}
                  disabled={dispatching}
                >
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
