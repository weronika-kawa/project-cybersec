let csrf;
export async function api(path, method = "GET", body) {
    if (method !== "GET" && !csrf) {
        const r = await fetch("/api/auth/csrf", { credentials: "same-origin" });
        if (!r.ok)
            throw Error("Nie można uzyskać ochrony CSRF.");
        csrf = (await r.json()).token;
    }
    const r = await fetch("/api" + path, { method, credentials: "same-origin", headers: { "Content-Type": "application/json", ...(method !== "GET" ? { "X-CSRF-TOKEN": csrf } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
    const data = await r.json().catch(() => ({}));
    if (!r.ok) {
        if (r.status === 401) {
            csrf = null;
            window.dispatchEvent(new Event("securebank:unauthorized"));
        }
        const e = Error(data.message || Object.values(data.errors || {}).flat().join(" ") || ({ 401: "Sesja wygasła lub konto jest zablokowane.", 403: "Brak uprawnień.", 404: "Nie znaleziono zasobu.", 429: "Zbyt wiele prób. Spróbuj za minutę." }[r.status]) || "Operacja nie powiodła się.");
        e.status = r.status;
        throw e;
    }
    return data;
}
export function clearCsrf() {
    csrf = null;
}
