/** "Waiting 3 h", "Waiting 2 d 4 h": how long a request has been open. */
export function waitingText(createdAt: string, now: Date = new Date()): string {
  const minutes = Math.max(0, Math.floor((now.getTime() - new Date(createdAt).getTime()) / 60000));
  if (minutes < 60) return `${Math.max(minutes, 1)} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h`;
  const days = Math.floor(hours / 24);
  const rest = hours % 24;
  return rest === 0 ? `${days} d` : `${days} d ${rest} h`;
}

/** Hours from now until the deadline. Negative means overdue. Null when no deadline was given. */
export function hoursUntil(neededBy: string | null, now: Date = new Date()): number | null {
  if (!neededBy) return null;
  return (new Date(neededBy).getTime() - now.getTime()) / 3600000;
}

export function deadlineText(neededBy: string | null, now: Date = new Date()): string {
  const hours = hoursUntil(neededBy, now);
  if (hours === null) return "No deadline given";
  if (hours <= 0) return `Overdue by ${waitingText(neededBy!, now)}`;
  if (hours < 1) return "Due within the hour";
  if (hours < 48) return `Due in ${Math.round(hours)} h`;
  return `Due in ${Math.round(hours / 24)} d`;
}

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString("en-GB", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

export const DISTRICTS = [
  "Ampara", "Anuradhapura", "Badulla", "Batticaloa", "Colombo", "Galle", "Gampaha", "Hambantota", "Jaffna",
  "Kalutara", "Kandy", "Kegalle", "Kilinochchi", "Kurunegala", "Mannar", "Matale", "Matara", "Monaragala",
  "Mullaitivu", "Nuwara Eliya", "Polonnaruwa", "Puttalam", "Ratnapura", "Trincomalee", "Vavuniya",
];
