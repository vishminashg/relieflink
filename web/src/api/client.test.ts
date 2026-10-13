import { api, ApiError, setToken, setUnauthorizedHandler } from "./client";
import { mockFetch } from "../test/fixtures";

afterEach(() => { setToken(null); setUnauthorizedHandler(() => {}); });

describe("api client", () => {
  it("sends the bearer token and leaves out empty filters", async () => {
    const { fn } = mockFetch({ "GET /api/relief-requests": { items: [] } });
    setToken("abc");
    await api.requests({ status: "", urgency: "Critical", page: 2 });
    const [url, init] = fn.mock.calls[0];
    expect(String(url)).toContain("urgency=Critical");
    expect(String(url)).toContain("page=2");
    expect(String(url)).not.toContain("status=");
    expect((init!.headers as Record<string, string>).Authorization).toBe("Bearer abc");
  });

  it("sends no Authorization header before sign in", async () => {
    const { fn } = mockFetch({ "POST /api/auth/login": { token: "t" } });
    await api.login("a@b.lk", "pw");
    expect((fn.mock.calls[0][1]!.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it("turns a problem-details response into a readable ApiError", async () => {
    mockFetch({ "POST /api/workflows/w1/revise": new Response(JSON.stringify({ title: "Comment required", errors: { comment: ["Too short"] } }), { status: 400 }) });
    const error = await api.revise("w1", "x").catch((e) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect(error.message).toBe("Comment required");
    expect(error.fields.comment).toEqual(["Too short"]);
  });

  it("explains a 403 in plain words", async () => {
    setToken("abc");
    mockFetch({ "POST /api/workflows/w1/execute": new Response("{}", { status: 403 }) });
    await expect(api.execute("w1")).rejects.toMatchObject({ status: 403, message: "Your role is not allowed to do this." });
  });

  it("ends the session when a signed-in call returns 401", async () => {
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    setToken("abc");
    mockFetch({ "GET /api/relief-requests/summary": new Response("{}", { status: 401 }) });
    await expect(api.summary()).rejects.toMatchObject({ status: 401 });
    expect(handler).toHaveBeenCalledOnce();
  });

  it("does not trigger the unauthorized handler for a failed login", async () => {
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    mockFetch({ "POST /api/auth/login": new Response("{}", { status: 401 }) });
    await expect(api.login("a@b.lk", "bad")).rejects.toBeInstanceOf(ApiError);
    expect(handler).not.toHaveBeenCalled();
  });

  it("reports a network failure clearly", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fail"); }));
    await expect(api.summary()).rejects.toMatchObject({ status: 0, message: expect.stringContaining("Cannot reach the server") });
  });

  it("posts the reject body the API expects", async () => {
    const { calls } = mockFetch({ "POST /api/workflows/w1/reject": {} });
    await api.reject("w1", "not safe", true);
    expect(calls[0].body).toEqual({ comment: "not safe", rejectRequest: true });
  });
});
