import { expect, test } from "@playwright/test";

test("office employee confirms panic, previews evidence and sees command outcome separately", async ({ page }) => {
  const id = "b156ad51-2fa3-4eb2-96a1-cf4727df1111";
  let uploads = 0;
  let submitted = false;
  let request: Record<string, unknown> | null = null;
  const messages: Record<string, unknown>[] = [];
  await page.route("**/api/v1/**", async route => {
    const path = new URL(route.request().url()).pathname;
    const method = route.request().method();
    if (path === "/api/v1/auth/session") return route.fulfill({ json: { role: "Employee", displayName: "Nhân viên văn phòng" } });
    if (path.endsWith("/requests") && method === "POST") {
      const body = route.request().postDataJSON();
      expect(body.confirmed).toBe(true);
      expect(body).not.toHaveProperty("deviceId");
      submitted = true;
      request = { ...body, id, deviceId: id, deviceName: "Máy làm việc", userId: id, userName: "Nhân viên", status: "Approved", assignedTechnicianId: null, assignedTechnicianName: "IT hỗ trợ", catalogAppId: null, commandId: id, commandStatus: "Pending", commandMessage: null, appointmentAt: null, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString(), approvalExpiresAt: null };
      return route.fulfill({ json: request });
    }
    if (path.endsWith("/requests")) return route.fulfill({ json: request ? [request] : [] });
    if (path.endsWith(`/requests/${id}`)) return route.fulfill({ json: request });
    if (path.endsWith("/messages") && method === "POST") {
      const body = route.request().postDataJSON();
      const message = { id, requestId: id, authorId: id, authorName: "Nhân viên", body: body.body, createdAt: new Date().toISOString() };
      messages.push(message); return route.fulfill({ json: message });
    }
    if (path.endsWith("/messages")) return route.fulfill({ json: messages });
    if (path.endsWith("/attachments") && method === "POST") {
      uploads++;
      return route.fulfill({ json: { id, requestId: id, fileName: "error.png", contentType: "image/png", size: 68, createdAt: new Date().toISOString() } });
    }
    return route.fulfill({ json: [] });
  });
  await page.goto("/support");
  await expect(page.getByRole("button", { name: "Tôi gặp sự cố", exact: false }).first()).toBeVisible();
  await page.getByRole("button", { name: "Tôi nghi máy bị nhiễm virus", exact: true }).click();
  await expect(page.getByRole("button", { name: "Gửi yêu cầu", exact: true })).toBeDisabled();
  expect(submitted).toBe(false);
  await page.getByLabel("Tôi hiểu thao tác và xác nhận gửi yêu cầu cho máy được giao cho tôi.").check();
  await page.getByRole("button", { name: "Gửi yêu cầu", exact: true }).click();
  await expect(page.getByText("Đang chờ máy tính xác nhận. Bạn chưa cần gửi lại yêu cầu.")).toBeVisible();
  await expect(page.getByText("Agent đã xác nhận lệnh cô lập", { exact: false })).toHaveCount(0);
  await page.getByLabel("Tin nhắn", { exact: true }).fill("Máy đang hiện thông báo lạ, nhờ IT kiểm tra.");
  await page.getByRole("button", { name: "Gửi tin nhắn", exact: true }).click();
  await expect(page.getByText("Máy đang hiện thông báo lạ, nhờ IT kiểm tra.", { exact: true })).toBeVisible();
  await page.getByLabel("Chọn ảnh", { exact: true }).setInputFiles({ name: "error.png", mimeType: "image/png", buffer: Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=", "base64") });
  await expect(page.getByAltText("Ảnh xem trước: error.png")).toBeVisible();
  await expect(page.getByRole("button", { name: "Gửi ảnh", exact: true })).toBeDisabled();
  expect(uploads).toBe(0);
  await page.getByLabel("Tôi đã xem lại ảnh và đồng ý gửi cho IT.").check();
  await page.getByRole("button", { name: "Gửi ảnh", exact: true }).click();
  await expect.poll(() => uploads).toBe(1);
});
