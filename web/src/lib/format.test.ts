import { DISTRICTS, deadlineText, hoursUntil, waitingText } from "./format";

const now = new Date("2026-10-13T12:00:00Z");
const ago = (minutes: number) => new Date(now.getTime() - minutes * 60000).toISOString();
const ahead = (hours: number) => new Date(now.getTime() + hours * 3600000).toISOString();

describe("waitingText", () => {
  it("shows minutes, hours, then days", () => {
    expect(waitingText(ago(0), now)).toBe("1 min");
    expect(waitingText(ago(45), now)).toBe("45 min");
    expect(waitingText(ago(60), now)).toBe("1 h");
    expect(waitingText(ago(60 * 23 + 59), now)).toBe("23 h");
    expect(waitingText(ago(60 * 24), now)).toBe("1 d");
    expect(waitingText(ago(60 * 52), now)).toBe("2 d 4 h");
  });
  it("never goes negative for a timestamp slightly in the future", () => {
    expect(waitingText(ahead(1), now)).toBe("1 min");
  });
});

describe("deadlines", () => {
  it("is null when there is no deadline", () => {
    expect(hoursUntil(null, now)).toBeNull();
    expect(deadlineText(null, now)).toBe("No deadline given");
  });
  it("describes overdue, imminent and later deadlines", () => {
    expect(deadlineText(ahead(-3), now)).toBe("Overdue by 3 h");
    expect(deadlineText(ahead(0.5), now)).toBe("Due within the hour");
    expect(deadlineText(ahead(5), now)).toBe("Due in 5 h");
    expect(deadlineText(ahead(72), now)).toBe("Due in 3 d");
  });
});

describe("DISTRICTS", () => {
  it("lists the 25 districts without duplicates", () => {
    expect(DISTRICTS).toHaveLength(25);
    expect(new Set(DISTRICTS).size).toBe(25);
  });
});
