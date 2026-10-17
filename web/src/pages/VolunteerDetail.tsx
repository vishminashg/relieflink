import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiError } from "../api/client";
import type {
  AssignmentStatus,
  AvailabilityType,
  SkillProficiency,
  VolunteerAssignment,
  VolunteerAvailability,
  VolunteerSkill,
  VolunteerSkillType,
} from "../api/types";
import { ErrorBanner, Loading } from "../components/Feedback";
import { DISTRICTS, formatDateTime, VOLUNTEER_SKILLS, skillLabel } from "../lib/format";
import { useLoad } from "../lib/useLoad";

const toApiError = (error: unknown, fallback: string) =>
  error instanceof ApiError ? error : new ApiError(0, fallback);

export function VolunteerDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data: volunteer, error, loading, reload } = useLoad(
    () => api.volunteer(id!),
    [id]
  );

  // Profile Edit State
  const [isEditingProfile, setIsEditingProfile] = useState(false);
  const [profileDistrict, setProfileDistrict] = useState("");
  const [profileEmergencyName, setProfileEmergencyName] = useState("");
  const [profileEmergencyPhone, setProfileEmergencyPhone] = useState("");
  const [profileMaxHours, setProfileMaxHours] = useState(40);
  const [profileNotes, setProfileNotes] = useState("");
  const [profileError, setProfileError] = useState<ApiError | null>(null);
  const [profileSaving, setProfileSaving] = useState(false);

  // Add Skill Form State
  const [showAddSkill, setShowAddSkill] = useState(false);
  const [newSkill, setNewSkill] = useState<VolunteerSkillType>("FirstAid");
  const [newProficiency, setNewProficiency] = useState<SkillProficiency>("Intermediate");
  const [newCertRef, setNewCertRef] = useState("");
  const [skillSaving, setSkillSaving] = useState(false);
  const [skillError, setSkillError] = useState<ApiError | null>(null);

  // Add Availability Form State
  const [showAddAvailability, setShowAddAvailability] = useState(false);
  const [availType, setAvailType] = useState<AvailabilityType>("RecurringWeekly");
  const [availDay, setAvailDay] = useState("Monday");
  const [availStartTime, setAvailStartTime] = useState("09:00");
  const [availEndTime, setAvailEndTime] = useState("17:00");
  const [availSpecificStart, setAvailSpecificStart] = useState("");
  const [availSpecificEnd, setAvailSpecificEnd] = useState("");
  const [availSaving, setAvailSaving] = useState(false);
  const [availError, setAvailError] = useState<ApiError | null>(null);

  // Assignment action state
  const [assignmentActionError, setAssignmentActionError] = useState<ApiError | null>(null);
  const [cancelModalAssignmentId, setCancelModalAssignmentId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState("");

  if (loading && !volunteer) return <Loading />;
  if (error || !volunteer) return <ErrorBanner error={error} onRetry={reload} />;

  // Start editing profile with current values
  const startEditProfile = () => {
    setProfileDistrict(volunteer.district);
    setProfileEmergencyName(volunteer.emergencyContactName);
    setProfileEmergencyPhone(volunteer.emergencyContactPhone);
    setProfileMaxHours(volunteer.maxHoursPerWeek);
    setProfileNotes(volunteer.notes ?? "");
    setIsEditingProfile(true);
    setProfileError(null);
  };

  const saveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setProfileSaving(true);
    setProfileError(null);
    try {
      await api.updateVolunteer(volunteer.id, {
        district: profileDistrict,
        emergencyContactName: profileEmergencyName,
        emergencyContactPhone: profileEmergencyPhone,
        maxHoursPerWeek: Number(profileMaxHours),
        notes: profileNotes || undefined,
      });
      setIsEditingProfile(false);
      reload();
    } catch (err) {
      setProfileError(toApiError(err, "The profile update failed."));
    } finally {
      setProfileSaving(false);
    }
  };

  const handleAddSkill = async (e: React.FormEvent) => {
    e.preventDefault();
    setSkillSaving(true);
    setSkillError(null);
    try {
      const existing = volunteer.skills.map((s) => ({
        skill: s.skill,
        proficiency: s.proficiency,
        certificateReference: s.certificateReference,
        isVerified: s.isVerified,
      }));
      // If skill already exists, update proficiency/cert
      const updated = existing.filter((s) => s.skill !== newSkill);
      updated.push({
        skill: newSkill,
        proficiency: newProficiency,
        certificateReference: newCertRef.trim() || null,
        isVerified: false,
      });

      await api.updateSkills(volunteer.id, updated);
      setShowAddSkill(false);
      setNewCertRef("");
      reload();
    } catch (err) {
      setSkillError(toApiError(err, "The skill update failed."));
    } finally {
      setSkillSaving(false);
    }
  };

  const handleToggleVerifySkill = async (skill: VolunteerSkill) => {
    setSkillError(null);
    try {
      const updated = volunteer.skills.map((s) => ({
        skill: s.skill,
        proficiency: s.proficiency,
        certificateReference: s.certificateReference,
        isVerified: s.skill === skill.skill ? !s.isVerified : s.isVerified,
      }));
      await api.updateSkills(volunteer.id, updated);
      reload();
    } catch (err) {
      setSkillError(toApiError(err, "The skill verification failed."));
    }
  };

  const handleRemoveSkill = async (skillType: VolunteerSkillType) => {
    setSkillError(null);
    try {
      const updated = volunteer.skills
        .filter((s) => s.skill !== skillType)
        .map((s) => ({
          skill: s.skill,
          proficiency: s.proficiency,
          certificateReference: s.certificateReference,
          isVerified: s.isVerified,
        }));
      await api.updateSkills(volunteer.id, updated);
      reload();
    } catch (err) {
      setSkillError(toApiError(err, "The skill removal failed."));
    }
  };

  const handleAddAvailability = async (e: React.FormEvent) => {
    e.preventDefault();
    setAvailSaving(true);
    setAvailError(null);
    try {
      const existing: Array<Omit<VolunteerAvailability, "id">> = volunteer.availabilities.map((a) => ({
        type: a.type,
        dayOfWeek: a.dayOfWeek,
        startTime: a.startTime,
        endTime: a.endTime,
        specificStartUtc: a.specificStartUtc,
        specificEndUtc: a.specificEndUtc,
      }));

      if (availType === "RecurringWeekly") {
        existing.push({
          type: "RecurringWeekly",
          dayOfWeek: availDay,
          startTime: `${availStartTime}:00`,
          endTime: `${availEndTime}:00`,
          specificStartUtc: null,
          specificEndUtc: null,
        });
      } else {
        if (!availSpecificStart || !availSpecificEnd) {
          throw new ApiError(400, "Start date/time and end date/time are required.");
        }
        existing.push({
          type: "SpecificDate",
          dayOfWeek: null,
          startTime: null,
          endTime: null,
          specificStartUtc: new Date(availSpecificStart).toISOString(),
          specificEndUtc: new Date(availSpecificEnd).toISOString(),
        });
      }

      await api.updateAvailability(volunteer.id, existing);
      setShowAddAvailability(false);
      reload();
    } catch (err) {
      setAvailError(toApiError(err, "The availability update failed."));
    } finally {
      setAvailSaving(false);
    }
  };

  const handleRemoveAvailability = async (availId: string) => {
    setAvailError(null);
    try {
      const updated = volunteer.availabilities
        .filter((a) => a.id !== availId)
        .map((a) => ({
          type: a.type,
          dayOfWeek: a.dayOfWeek,
          startTime: a.startTime,
          endTime: a.endTime,
          specificStartUtc: a.specificStartUtc,
          specificEndUtc: a.specificEndUtc,
        }));
      await api.updateAvailability(volunteer.id, updated);
      reload();
    } catch (err) {
      setAvailError(toApiError(err, "The availability removal failed."));
    }
  };

  const handleUpdateAssignmentStatus = async (
    assignmentId: string,
    newStatus: AssignmentStatus
  ) => {
    setAssignmentActionError(null);
    try {
      await api.updateAssignmentStatus(assignmentId, newStatus);
      reload();
    } catch (err) {
      setAssignmentActionError(toApiError(err, "The assignment status update failed."));
    }
  };

  const handleCancelAssignment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cancelModalAssignmentId) return;
    setAssignmentActionError(null);
    try {
      await api.cancelAssignment(cancelModalAssignmentId, cancelReason.trim());
      setCancelModalAssignmentId(null);
      setCancelReason("");
      reload();
    } catch (err) {
      setAssignmentActionError(toApiError(err, "The assignment cancellation failed."));
    }
  };

  const DAYS = [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday",
  ];

  return (
    <>
      <p className="crumbs">
        <Link to="/volunteers">&larr; Volunteers</Link>
      </p>

      <header className="page-head">
        <div className="page-head-row">
          <div>
            <h1>{volunteer.user?.fullName ?? "Volunteer Profile"}</h1>
            <div className="head-tags">
              <span
                className={`pill ${
                  volunteer.isActive ? "status-approved" : "status-rejected"
                }`}
              >
                {volunteer.isActive ? "Active" : "Inactive"}
              </span>
              <span className="tag">{volunteer.district}</span>
              {volunteer.activeAssignmentsCount > 0 ? (
                <span className="pill status-inprogress">On Active Mission</span>
              ) : (
                <span className="available-badge">Available for Dispatch</span>
              )}
            </div>
          </div>
          <div className="button-row">
            <Link
              className="btn btn-primary"
              to={`/volunteers/match?district=${volunteer.district}`}
            >
              Match &amp; Dispatch
            </Link>
            {!isEditingProfile && (
              <button
                className="btn btn-quiet"
                onClick={startEditProfile}
                type="button"
              >
                Edit Profile
              </button>
            )}
          </div>
        </div>
      </header>

      <ErrorBanner error={assignmentActionError} />

      <div className="detail-grid">
        <div className="stack">
          {/* Section: Profile Info / Edit Form */}
          <section className="panel" aria-labelledby="profile-heading">
            <h2 id="profile-heading">Profile Details</h2>

            {isEditingProfile ? (
              <form onSubmit={saveProfile}>
                <ErrorBanner error={profileError} />
                <div className="form-row stacked">
                  <label className="grow">
                    District
                    <select
                      value={profileDistrict}
                      onChange={(e) => setProfileDistrict(e.target.value)}
                      required
                    >
                      {DISTRICTS.map((d) => (
                        <option key={d} value={d}>
                          {d}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Max Hours / Week
                    <input
                      type="number"
                      min="1"
                      max="168"
                      value={profileMaxHours}
                      onChange={(e) => setProfileMaxHours(Number(e.target.value))}
                      required
                    />
                  </label>
                </div>

                <div className="form-row stacked">
                  <label className="grow">
                    Emergency Contact Name (ICE)
                    <input
                      type="text"
                      value={profileEmergencyName}
                      onChange={(e) => setProfileEmergencyName(e.target.value)}
                      required
                    />
                  </label>
                  <label className="grow">
                    Emergency Contact Phone
                    <input
                      type="tel"
                      value={profileEmergencyPhone}
                      onChange={(e) => setProfileEmergencyPhone(e.target.value)}
                      required
                    />
                  </label>
                </div>

                <div className="stacked">
                  <label>
                    Operational Notes
                    <textarea
                      rows={3}
                      value={profileNotes}
                      onChange={(e) => setProfileNotes(e.target.value)}
                      placeholder="Special accommodations, vehicle details, radio callsign..."
                    />
                  </label>
                </div>

                <div className="button-row">
                  <button
                    className="btn btn-primary"
                    type="submit"
                    disabled={profileSaving}
                  >
                    {profileSaving ? "Saving..." : "Save Profile"}
                  </button>
                  <button
                    className="btn btn-quiet"
                    type="button"
                    onClick={() => setIsEditingProfile(false)}
                    disabled={profileSaving}
                  >
                    Cancel
                  </button>
                </div>
              </form>
            ) : (
              <dl className="facts">
                <div>
                  <dt>Email</dt>
                  <dd>{volunteer.user?.email ?? "—"}</dd>
                </div>
                <div>
                  <dt>Phone</dt>
                  <dd>{volunteer.user?.phone ?? "Not provided"}</dd>
                </div>
                <div>
                  <dt>Base District</dt>
                  <dd>{volunteer.district}</dd>
                </div>
                <div>
                  <dt>In Case of Emergency (ICE)</dt>
                  <dd>
                    {volunteer.emergencyContactName} ({volunteer.emergencyContactPhone})
                  </dd>
                </div>
                <div>
                  <dt>Weekly Commitment Limit</dt>
                  <dd>{volunteer.maxHoursPerWeek} hours / week</dd>
                </div>
                {volunteer.notes && (
                  <div>
                    <dt>Operational Notes</dt>
                    <dd className="notes">{volunteer.notes}</dd>
                  </div>
                )}
              </dl>
            )}
          </section>

          {/* Section: Certified Skills */}
          <section className="panel" aria-labelledby="skills-heading">
            <div className="panel-head">
              <h2 id="skills-heading">
                Skills &amp; Qualifications ({volunteer.skills.length})
              </h2>
              <button
                className="btn btn-quiet"
                onClick={() => setShowAddSkill(!showAddSkill)}
                type="button"
              >
                {showAddSkill ? "Cancel" : "+ Add Skill"}
              </button>
            </div>

            <ErrorBanner error={skillError} />

            {showAddSkill && (
              <form className="inline-form" onSubmit={handleAddSkill}>
                <div className="form-row stacked">
                  <label className="grow">
                    Skill
                    <select
                      value={newSkill}
                      onChange={(e) =>
                        setNewSkill(e.target.value as VolunteerSkillType)
                      }
                    >
                      {VOLUNTEER_SKILLS.map((s) => (
                        <option key={s} value={s}>
                          {skillLabel(s)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Proficiency
                    <select
                      value={newProficiency}
                      onChange={(e) =>
                        setNewProficiency(e.target.value as SkillProficiency)
                      }
                    >
                      <option value="Beginner">Beginner</option>
                      <option value="Intermediate">Intermediate</option>
                      <option value="Advanced">Advanced</option>
                      <option value="Expert">Expert</option>
                    </select>
                  </label>
                </div>
                <div className="stacked">
                  <label>
                    Certificate / License Reference (Optional)
                    <input
                      type="text"
                      value={newCertRef}
                      onChange={(e) => setNewCertRef(e.target.value)}
                      placeholder="e.g. SL Red Cross BLS-2026-889"
                    />
                  </label>
                </div>
                <button
                  className="btn btn-primary"
                  type="submit"
                  disabled={skillSaving}
                >
                  {skillSaving ? "Saving..." : "Add Certified Skill"}
                </button>
              </form>
            )}

            {volunteer.skills.length === 0 ? (
              <p className="muted">No certified skills registered yet.</p>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>Skill</th>
                    <th>Proficiency</th>
                    <th>Certification</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {volunteer.skills.map((s) => (
                    <tr key={s.id}>
                      <td>
                        <strong>{skillLabel(s.skill)}</strong>
                      </td>
                      <td>{s.proficiency}</td>
                      <td>{s.certificateReference ?? "—"}</td>
                      <td>
                        {s.isVerified ? (
                          <span className="badge-verified">Verified ✓</span>
                        ) : (
                          <span className="badge-unverified">Unverified</span>
                        )}
                      </td>
                      <td>
                        <button
                          className="btn btn-quiet"
                          style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                          onClick={() => handleToggleVerifySkill(s)}
                          type="button"
                          title="Toggle coordinator verification"
                        >
                          {s.isVerified ? "Revoke" : "Verify"}
                        </button>
                        {" "}
                        <button
                          className="btn btn-danger"
                          style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                          onClick={() => handleRemoveSkill(s.skill)}
                          type="button"
                          title="Remove skill"
                        >
                          Remove
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>

          {/* Section: Availability Windows */}
          <section className="panel" aria-labelledby="avail-heading">
            <div className="panel-head">
              <h2 id="avail-heading">
                Availability Windows ({volunteer.availabilities.length})
              </h2>
              <button
                className="btn btn-quiet"
                onClick={() => setShowAddAvailability(!showAddAvailability)}
                type="button"
              >
                {showAddAvailability ? "Cancel" : "+ Add Slot"}
              </button>
            </div>

            <ErrorBanner error={availError} />

            {showAddAvailability && (
              <form className="inline-form" onSubmit={handleAddAvailability}>
                <div className="form-row stacked">
                  <label>
                    Type
                    <select
                      value={availType}
                      onChange={(e) =>
                        setAvailType(e.target.value as AvailabilityType)
                      }
                    >
                      <option value="RecurringWeekly">Recurring Weekly</option>
                      <option value="SpecificDate">Specific Date Window</option>
                    </select>
                  </label>

                  {availType === "RecurringWeekly" ? (
                    <>
                      <label>
                        Day of Week
                        <select
                          value={availDay}
                          onChange={(e) => setAvailDay(e.target.value)}
                        >
                          {DAYS.map((d) => (
                            <option key={d} value={d}>
                              {d}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label>
                        Start Time
                        <input
                          type="time"
                          value={availStartTime}
                          onChange={(e) => setAvailStartTime(e.target.value)}
                          required
                        />
                      </label>
                      <label>
                        End Time
                        <input
                          type="time"
                          value={availEndTime}
                          onChange={(e) => setAvailEndTime(e.target.value)}
                          required
                        />
                      </label>
                    </>
                  ) : (
                    <>
                      <label className="grow">
                        Start Date &amp; Time
                        <input
                          type="datetime-local"
                          value={availSpecificStart}
                          onChange={(e) => setAvailSpecificStart(e.target.value)}
                          required
                        />
                      </label>
                      <label className="grow">
                        End Date &amp; Time
                        <input
                          type="datetime-local"
                          value={availSpecificEnd}
                          onChange={(e) => setAvailSpecificEnd(e.target.value)}
                          required
                        />
                      </label>
                    </>
                  )}
                </div>

                <button
                  className="btn btn-primary"
                  type="submit"
                  disabled={availSaving}
                >
                  {availSaving ? "Saving..." : "Add Availability Window"}
                </button>
              </form>
            )}

            {volunteer.availabilities.length === 0 ? (
              <p className="muted">No recurring or specific availability registered.</p>
            ) : (
              <ul className="plain-list">
                {volunteer.availabilities.map((a) => (
                  <li
                    key={a.id}
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                    }}
                  >
                    <span>
                      {a.type === "RecurringWeekly" ? (
                        <>
                          <strong>Every {a.dayOfWeek}:</strong> {a.startTime?.slice(0, 5)} - {a.endTime?.slice(0, 5)}
                        </>
                      ) : (
                        <>
                          <strong>Specific Window:</strong>{" "}
                          {a.specificStartUtc ? formatDateTime(a.specificStartUtc) : "—"} &rarr;{" "}
                          {a.specificEndUtc ? formatDateTime(a.specificEndUtc) : "—"}
                        </>
                      )}
                    </span>
                    <button
                      className="btn btn-danger"
                      style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                      onClick={() => handleRemoveAvailability(a.id)}
                      type="button"
                    >
                      Delete
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          {/* Section: Assignment History */}
          <section className="panel" aria-labelledby="history-heading">
            <h2 id="history-heading">
              Deployment &amp; Mission History ({volunteer.activeAssignmentsCount} active)
            </h2>

            {volunteer.activeAssignmentsCount > 0 && (
              <div className="override-note">
                <strong>Schedule Guarantee Active:</strong> This volunteer is currently deployed. Overlapping assignments (+ 30-minute transit buffer) will be blocked to strictly prevent double-booking.
              </div>
            )}

            {/* Assignments list */}
            <AssignmentHistoryList
              volunteerId={volunteer.id}
              onUpdateStatus={handleUpdateAssignmentStatus}
              onRequestCancel={(assignId) => setCancelModalAssignmentId(assignId)}
            />
          </section>
        </div>

        {/* Sidebar Summary Facts */}
        <aside className="stack">
          <div className="panel">
            <h2>Deployment Summary</h2>
            <dl className="facts">
              <div>
                <dt>Active Missions</dt>
                <dd>
                  {volunteer.activeAssignmentsCount > 0 ? (
                    <span className="pill status-inprogress">
                      {volunteer.activeAssignmentsCount} active
                    </span>
                  ) : (
                    <span className="available-badge">0 active</span>
                  )}
                </dd>
              </div>
              <div>
                <dt>District Operational Base</dt>
                <dd>{volunteer.district}</dd>
              </div>
              <div>
                <dt>Registered Date</dt>
                <dd>{formatDateTime(volunteer.createdAt)}</dd>
              </div>
              <div>
                <dt>Last Profile Update</dt>
                <dd>{formatDateTime(volunteer.updatedAt)}</dd>
              </div>
            </dl>
          </div>

          <div className="panel">
            <h2>Quick Actions</h2>
            <div className="button-row">
              <button
                className="btn btn-primary"
                style={{ width: "100%" }}
                onClick={() =>
                  navigate(`/volunteers/match?district=${volunteer.district}`)
                }
                type="button"
              >
                Match for Nearby Disasters
              </button>
            </div>
          </div>
        </aside>
      </div>

      {/* Cancel Assignment Modal / Form */}
      {cancelModalAssignmentId && (
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
          <div className="login-card" style={{ maxWidth: "30rem" }}>
            <h2>Cancel Volunteer Mission</h2>
            <p className="muted">
              Provide an operational rationale for cancelling this field assignment.
            </p>
            <form onSubmit={handleCancelAssignment}>
              <div className="stacked">
                <label>
                  Cancellation Reason
                  <textarea
                    rows={3}
                    value={cancelReason}
                    onChange={(e) => setCancelReason(e.target.value)}
                    placeholder="e.g. Request fulfilled by local council, or volunteer fell ill"
                    required
                  />
                </label>
              </div>
              <div className="button-row">
                <button className="btn btn-danger" type="submit">
                  Confirm Cancellation
                </button>
                <button
                  className="btn btn-quiet"
                  type="button"
                  onClick={() => {
                    setCancelModalAssignmentId(null);
                    setCancelReason("");
                  }}
                >
                  Dismiss
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}

function AssignmentHistoryList({
  volunteerId,
  onUpdateStatus,
  onRequestCancel,
}: {
  volunteerId: string;
  onUpdateStatus: (id: string, status: AssignmentStatus) => void;
  onRequestCancel: (id: string) => void;
}) {
  const { data, loading, error, reload } = useLoad(
    () => api.assignments({ volunteerId, pageSize: 50 }),
    [volunteerId]
  );

  if (loading && !data) return <p className="muted">Loading mission records...</p>;
  if (error) return <ErrorBanner error={error} onRetry={reload} />;
  if (!data || data.items.length === 0) {
    return <p className="muted">No historical missions recorded for this volunteer.</p>;
  }

  return (
    <table className="table">
      <thead>
        <tr>
          <th>Time Window</th>
          <th>Role / Task</th>
          <th>Status</th>
          <th>Relief Request</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {data.items.map((a: VolunteerAssignment) => (
          <tr key={a.id}>
            <td>
              <div>{formatDateTime(a.startTime)}</div>
              <div className="muted">&rarr; {formatDateTime(a.endTime)}</div>
            </td>
            <td>
              <strong>{a.roleOrTask ?? "General Relief"}</strong>
              {a.notes && <div className="muted" style={{ fontSize: "0.85rem" }}>{a.notes}</div>}
            </td>
            <td>
              <span className={`pill status-${a.status.toLowerCase()}`}>
                {a.status}
              </span>
            </td>
            <td>
              {a.reliefRequestId ? (
                <Link to={`/requests/${a.reliefRequestId}`}>
                  View Request
                </Link>
              ) : (
                <span className="muted">Field standby</span>
              )}
            </td>
            <td>
              {a.status === "Assigned" && (
                <div className="button-row" style={{ marginTop: 0 }}>
                  <button
                    className="btn btn-primary"
                    style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                    onClick={() => onUpdateStatus(a.id, "InProgress")}
                    type="button"
                  >
                    Start Mission
                  </button>
                  <button
                    className="btn btn-danger"
                    style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                    onClick={() => onRequestCancel(a.id)}
                    type="button"
                  >
                    Cancel
                  </button>
                </div>
              )}
              {a.status === "InProgress" && (
                <div className="button-row" style={{ marginTop: 0 }}>
                  <button
                    className="btn btn-primary"
                    style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                    onClick={() => onUpdateStatus(a.id, "Completed")}
                    type="button"
                  >
                    Complete Mission
                  </button>
                  <button
                    className="btn btn-danger"
                    style={{ padding: "0.2rem 0.5rem", minHeight: "auto", fontSize: "0.85rem" }}
                    onClick={() => onRequestCancel(a.id)}
                    type="button"
                  >
                    Cancel
                  </button>
                </div>
              )}
              {(a.status === "Completed" || a.status === "Cancelled") && (
                <span className="muted" style={{ fontSize: "0.85rem" }}>Finished</span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
