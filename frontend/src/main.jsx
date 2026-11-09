import React, { useState, useEffect, createContext, useContext } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Routes, Route, NavLink, Navigate, useNavigate, useParams, Link } from "react-router-dom";
import { ShieldCheck, LayoutDashboard, Wallet, ArrowUpRight, History, UserRound, Users, ScrollText, Bell, LogOut, LockKeyhole, ChevronRight } from "lucide-react";
import { api, clearCsrf } from "./api";
import "./style.css";
const Auth = createContext(null);
const money = v => new Intl.NumberFormat("pl-PL", { style: "currency", currency: "PLN" }).format(v);
const date = v => new Date(v).toLocaleString("pl-PL");
function App() {
    const [user, setUser] = useState(null), [ready, setReady] = useState(false);
    useEffect(() => {
        const expired = () => setUser(null);
        window.addEventListener("securebank:unauthorized", expired);
        api("/auth/me").then(setUser).catch(() => {
        }).finally(() => setReady(true));
        return () => window.removeEventListener("securebank:unauthorized", expired);
    }, []);
    if (!ready)
        return <div className="loading">Łączenie z SecureBank…</div>;
    return <Auth.Provider value={{ user, setUser }}><Routes><Route path="/login" element={<Login />}/><Route path="/register" element={<Login register/>}/><Route path="/reset" element={<Reset />}/><Route path="/*" element={user ? <Shell /> : <Navigate to="/login" replace/>}/></Routes></Auth.Provider>;
}
function Message({ error, success }) {
    return <>{error && <div className="message error" role="alert">{error}</div>}{success && <div className="message success" role="status">{success}</div>}</>;
}
function Field({ label, ...props }) {
    return <label>{label}<input {...props}/></label>;
}
function Login({ register = false }) {
    const { user, setUser } = useContext(Auth);
    const nav = useNavigate();
    const [error, setError] = useState(""), [busy, setBusy] = useState(false), [success, setSuccess] = useState("");
    if (user)
        return <Navigate to="/"/>;
    async function submit(e) {
        e.preventDefault();
        setError("");
        setBusy(true);
        const f = Object.fromEntries(new FormData(e.target));
        try {
            const r = await api("/auth/" + (register ? "register" : "login"), "POST", f);
            clearCsrf();
            if (register) {
                setSuccess(r.message);
            }
            else {
                setUser(await api("/auth/me"));
                nav("/");
            }
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    return <div className="auth-page"><section className="auth-story"><div className="brand"><ShieldCheck size={32}/>SecureBank<span>LAB</span></div><div><span className="eyebrow">BEZPIECZEŃSTWO ZACZYNA SIĘ OD WERYFIKACJI</span><h1>Twoje finanse.<br />Każda operacja<br /><em>pod kontrolą.</em></h1><p>Laboratorium bankowości internetowej oparte na zasadach Zero Trust.</p><div className="trust-pills"><span>Identity</span><span>RBAC</span><span>Weryfikacja OTP</span></div></div><small>Projekt akademicki · Wyłącznie dane demonstracyjne</small></section><section className="auth-form"><div className="auth-box"><LockKeyhole className="icon-box"/><h2>{register ? "Utwórz konto" : "Witaj ponownie"}</h2><p>{register ? "Rozpocznij z własnym rachunkiem demonstracyjnym." : "Zaloguj się do bezpiecznego panelu."}</p><Message error={error} success={success}/><form onSubmit={submit}>{register && <Field label="Imię / nazwa" name="displayName" required maxLength={80}/>}<Field label="Adres e-mail" name="email" type="email" autoComplete="username" required/><Field label="Hasło" name="password" type="password" autoComplete={register ? "new-password" : "current-password"} minLength={register ? 12 : undefined} maxLength={128} required/>{register && <small>Minimum 12 znaków: duża i mała litera, cyfra i znak specjalny.</small>}<button disabled={busy}>{busy ? "Proszę czekać…" : register ? "Zarejestruj się" : "Zaloguj się"}<ArrowUpRight size={18}/></button></form><p>{register ? "Masz już konto?" : "Pierwsza wizyta?"} <Link to={register ? "/login" : "/register"}>{register ? "Zaloguj się" : "Utwórz konto"}</Link></p><Link to="/reset">Reset hasła (demo)</Link><div className="lab-note"><ShieldCheck size={18}/><span>Sesja wygasa po 20 minutach. Operacje wysokiego ryzyka wymagają potwierdzenia.</span></div></div></section></div>;
}
function Reset() {
    const [token, setToken] = useState(""), [error, setError] = useState(""), [success, setSuccess] = useState(""), [busy, setBusy] = useState(false);
    async function submit(e) {
        e.preventDefault();
        setBusy(true);
        setError("");
        const f = Object.fromEntries(new FormData(e.target));
        try {
            const r = await api(token ? "/auth/reset-confirm" : "/auth/reset-request", "POST", token ? { ...f, token } : { email: f.email });
            if (r.demoToken)
                setToken(r.demoToken);
            setSuccess(r.message);
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    return <div className="reset"><div className="card"><h1>Reset hasła</h1><p>Tryb laboratoryjny: token przekazywany bezpośrednio zamiast przez e-mail.</p><Message error={error} success={success}/><form onSubmit={submit}><Field label="E-mail" name="email" type="email" required/>{token && <Field label="Nowe hasło" name="password" type="password" minLength={12} required/>}<button disabled={busy}>{token ? "Zmień hasło" : "Przygotuj reset"}</button></form><Link to="/login">Powrót do logowania</Link></div></div>;
}
function Shell() {
    const { user, setUser } = useContext(Auth);
    const nav = useNavigate();
    const [error, setError] = useState("");
    const admin = user.roles.includes("ADMIN"), client = user.roles.includes("CLIENT");
    const links = admin ? [["/", "Przegląd", LayoutDashboard], ["/admin/users", "Użytkownicy", Users], ["/admin/logs", "Logi bezpieczeństwa", ScrollText], ["/admin/alerts", "Alerty", Bell]] : client ? [["/", "Przegląd", LayoutDashboard], ["/accounts", "Rachunki", Wallet], ["/transfers", "Nowy przelew", ArrowUpRight], ["/history", "Historia transakcji", History]] : [["/", "Przegląd", LayoutDashboard], ["/users", "Klienci", Users]];
    async function logout() {
        try {
            await api("/auth/logout", "POST");
            clearCsrf();
            setUser(null);
            nav("/login");
        }
        catch (e) {
            if (e.status === 401) {
                setUser(null);
                nav("/login");
            }
            else
                setError(e.message);
        }
    }
    return <div className="app"><aside><Link className="brand" to="/"><ShieldCheck size={28}/>SecureBank<span>LAB</span></Link><div className="nav-label">TWÓJ PANEL</div><nav>{links.map(([to, label, Icon]) => <NavLink key={to} to={to} end={to === "/"}><Icon size={19}/>{label}</NavLink>)}<NavLink to="/profile"><UserRound size={19}/>Profil i bezpieczeństwo</NavLink></nav><div className="sidebar-bottom"><div className="secure-state"><ShieldCheck size={20}/><div>Zero Trust aktywne<small>Każdy dostęp jest weryfikowany</small></div></div><button className="ghost" onClick={logout}><LogOut size={18}/>Wyloguj się</button></div></aside><div className="workspace"><header><span>Bankowość demonstracyjna <span className="badge">SANDBOX</span></span><div className="identity"><span className="avatar">{user.displayName[0]?.toUpperCase()}</span><div>{user.displayName}<small>{user.roles.join(", ")}</small></div></div></header><main><Message error={error}/><Routes><Route path="/" element={<Dashboard />}/><Route path="/profile" element={<Profile />}/><Route path="/accounts" element={<Guard role="CLIENT"><Accounts /></Guard>}/><Route path="/accounts/:id" element={<Guard role="CLIENT"><AccountDetail /></Guard>}/><Route path="/transfers" element={<Guard role="CLIENT"><Transfers /></Guard>}/><Route path="/verification/:id" element={<Guard role="CLIENT"><Verification /></Guard>}/><Route path="/history" element={<Guard role="CLIENT"><HistoryPage /></Guard>}/><Route path="/users" element={<Guard role="EMPLOYEE"><Employee /></Guard>}/><Route path="/admin/users" element={<Guard role="ADMIN"><AdminUsers /></Guard>}/><Route path="/admin/logs" element={<Guard role="ADMIN"><AuditPage /></Guard>}/><Route path="/admin/alerts" element={<Guard role="ADMIN"><AuditPage alerts/></Guard>}/><Route path="*" element={<div className="card">Nie znaleziono strony.</div>}/></Routes></main><footer>SecureBank · Projekt akademicki <span>Żadne rzeczywiste środki nie są przetwarzane</span></footer></div></div>;
}
function Guard({ role, children }) {
    const { user } = useContext(Auth);
    return user.roles.includes(role) ? children : <div className="card"><h1>Brak uprawnień</h1><p>Ten widok wymaga roli {role}. API niezależnie sprawdza każde żądanie.</p></div>;
}
function useData(path) {
    const [data, setData] = useState(null), [error, setError] = useState(""), [loading, setLoading] = useState(true);
    async function refresh() {
        setLoading(true);
        setError("");
        try {
            setData(await api(path));
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setLoading(false);
        }
    }
    useEffect(() => {
        refresh();
    }, [path]);
    return { data, error, loading, refresh };
}
function State({ error, loading }) {
    return <><Message error={error}/>{loading && <div className="loading">Wczytywanie danych…</div>}</>;
}
function Title({ tag = "TWÓJ SECUREBANK", title, text, action }) {
    return <div className="page-title"><div><span className="eyebrow">{tag}</span><h1>{title}</h1>{text && <p>{text}</p>}</div>{action}</div>;
}
function Dashboard() {
    const { user } = useContext(Auth);
    if (user.roles.includes("ADMIN"))
        return <AdminDashboard />;
    if (user.roles.includes("EMPLOYEE"))
        return <Employee />;
    return <ClientDashboard />;
}
function ClientDashboard() {
    const { user } = useContext(Auth);
    const a = useData("/accounts"), t = useData("/transactions");
    return <><Title title={"Dzień dobry, " + user.displayName} text="Wszystko, czego potrzebujesz, w jednym bezpiecznym miejscu." action={<Link className="button" to="/transfers">Nowy przelew <ArrowUpRight size={18}/></Link>}/><State {...a}/>{a.data && <><div className="stats"><div className="balance-card"><span>Łączne dostępne środki</span><h2>{money(a.data.reduce((s, x) => s + x.balance, 0))}</h2><small>Środki demonstracyjne · PLN</small><Wallet size={58}/></div><div className="card stat"><span>Twoje rachunki</span><h2>{a.data.length.toString().padStart(2, "0")}</h2><small>Rachunki w systemie</small></div><div className="card stat"><span>Ochrona operacji</span><h2 className="green">Aktywna</h2><small><ShieldCheck size={15}/> Weryfikacja po stronie API</small></div></div><div className="section-heading"><h2>Twoje rachunki</h2><Link to="/accounts">Zobacz wszystkie <ChevronRight size={16}/></Link></div><div className="account-grid">{a.data.map(x => <AccountCard key={x.id} account={x}/>)}</div></>}<div className="section-heading"><h2>Ostatnie transakcje</h2><Link to="/history">Pełna historia <ChevronRight size={16}/></Link></div><State {...t}/>{t.data && <TransactionTable data={t.data.slice(0, 5)} accounts={a.data || []}/>}<div className="info-strip"><ShieldCheck /><div><strong>Zaufanie nigdy nie jest domyślne.</strong><p>Własność rachunku, rola i saldo są sprawdzane przy każdej operacji. Przelewy od 5 000 PLN wymagają OTP.</p></div></div></>;
}
function AccountCard({ account: x }) {
    return <Link to={"/accounts/" + x.id} className="card account"><div><Wallet size={21}/><span className="badge">{x.isActive ? "AKTYWNY" : "NIEAKTYWNY"}</span></div><h3>Rachunek osobisty</h3><code>{x.accountNumber}</code><h2>{money(x.balance)}</h2><small>Zobacz szczegóły <ArrowUpRight size={15}/></small></Link>;
}
function Accounts() {
    const q = useData("/accounts");
    return <><Title title="Twoje rachunki" text="Dostęp wyłącznie do rachunków należących do Ciebie."/><State {...q}/><div className="account-grid">{q.data?.map(x => <AccountCard key={x.id} account={x}/>)}</div></>;
}
function AccountDetail() {
    const { id } = useParams();
    const q = useData("/accounts/" + id), t = useData("/transactions?accountId=" + id);
    return <><Title title="Szczegóły rachunku"/><State {...q}/>{q.data && <><AccountCard account={q.data}/><div className="card"><h3>Dane rachunku</h3><p>ID: <code>{q.data.id}</code></p><p>Utworzono: {date(q.data.createdAt)}</p><p>Waluta: {q.data.currency}</p></div><h2>Historia rachunku</h2><State {...t}/>{t.data && <TransactionTable data={t.data} accounts={[q.data]}/>}</>}</>;
}
function TransactionTable({ data, accounts = [] }) {
    return <div className="card table-card"><table><thead><tr><th>Operacja / odbiorca</th><th>Data</th><th>Status</th><th className="right">Kwota</th></tr></thead><tbody>{data.map(x => {
        const outgoing = accounts.some(a => a.id === x.fromAccountId);
        return <tr key={x.id}><td><strong>{x.title}</strong><small>{outgoing ? x.to : x.from}</small></td><td>{date(x.timestamp)}</td><td><span className="badge">{x.status}</span></td><td className={"right " + (outgoing ? "" : "green")}>{outgoing ? "−" : "+"}{money(x.amount)}</td></tr>;
    })}</tbody></table>{!data.length && <div className="empty">Brak transakcji.</div>}</div>;
}
function HistoryPage() {
    const q = useData("/transactions"), a = useData("/accounts");
    return <><Title title="Historia transakcji" text="Ostatnie 200 operacji na Twoich rachunkach."/><State {...q}/>{q.data && <TransactionTable data={q.data} accounts={a.data || []}/>}</>;
}
function Transfers() {
    const q = useData("/accounts"), nav = useNavigate();
    const [error, setError] = useState(""), [success, setSuccess] = useState(""), [busy, setBusy] = useState(false);
    async function submit(e) {
        e.preventDefault();
        setBusy(true);
        setError("");
        setSuccess("");
        const f = Object.fromEntries(new FormData(e.target));
        try {
            const r = await api("/transactions", "POST", { ...f, amount: Number(f.amount) });
            if (r.challengeId) {
                sessionStorage.setItem("otp:" + r.challengeId, JSON.stringify({ code: r.demoCode, expiresAt: r.expiresAt }));
                nav("/verification/" + r.challengeId);
            }
            else {
                setSuccess("Przelew wykonany. Salda zaktualizowano atomowo.");
                q.refresh();
                e.target.reset();
            }
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    return <><Title title="Nowy przelew" text="Przelew wewnętrzny pomiędzy rachunkami SecureBank."/><State {...q}/><div className="two-col"><div className="card"><Message error={error} success={success}/><form onSubmit={submit}><label>Z rachunku<select name="fromAccountId" required>{q.data?.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.accountNumber} · {money(x.balance)}</option>)}</select></label><Field label="Numer rachunku odbiorcy" name="toAccountNumber" placeholder="SB…" maxLength={40} required/><div className="form-row"><Field label="Kwota (PLN)" name="amount" type="number" step="0.01" min="0.01" max="1000000000" required/><Field label="Tytuł przelewu" name="title" maxLength={140} required/></div><button disabled={busy || !q.data?.length}>{busy ? "Weryfikacja…" : "Dalej / wykonaj przelew"}<ArrowUpRight size={18}/></button></form></div><div className="card guidance"><ShieldCheck size={30}/><h2>Bezpieczeństwo przelewu</h2><p>Backend ponownie sprawdza właściciela, uprawnienia, dostępne saldo i aktywność rachunków.</p><p>Od 5 000 PLN wymagany jest jednorazowy kod. Kod wygasa po 3 minutach.</p><small>Odbiorca demo: SB00000000000000000000000002</small></div></div></>;
}
function Verification() {
    const { id } = useParams(), nav = useNavigate();
    const [error, setError] = useState(""), [busy, setBusy] = useState(false), [success, setSuccess] = useState("");
    const meta = JSON.parse(sessionStorage.getItem("otp:" + id) || "{}");
    async function submit(e) {
        e.preventDefault();
        setBusy(true);
        setError("");
        try {
            await api("/transactions/" + id + "/verify", "POST", { code: new FormData(e.target).get("code") });
            sessionStorage.removeItem("otp:" + id);
            setSuccess("Przelew potwierdzony i wykonany.");
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    return <><Title title="Potwierdzenie operacji" text="Dodatkowa weryfikacja przelewu wysokiego ryzyka."/><div className="card narrow"><LockKeyhole size={32}/><Message error={error} success={success}/>{success ? <button onClick={() => nav("/history")}>Zobacz historię</button> : <><p>Kod jest przypisany do dokładnie jednego przelewu.</p>{meta.code && <div className="lab-note">Kod laboratoryjny: <strong>{meta.code}</strong><br />Wygasa: {date(meta.expiresAt)}</div>}<form onSubmit={submit}><Field label="Kod OTP" name="code" inputMode="numeric" pattern="[0-9]{6}" maxLength={6} required autoComplete="one-time-code"/><button disabled={busy}>{busy ? "Potwierdzanie…" : "Potwierdź przelew"}</button></form></>}</div></>;
}
function Profile() {
    const { user, setUser } = useContext(Auth);
    const [error, setError] = useState(""), [busy, setBusy] = useState(false);
    async function submit(e) {
        e.preventDefault();
        setBusy(true);
        setError("");
        try {
            await api("/auth/password", "POST", Object.fromEntries(new FormData(e.target)));
            clearCsrf();
            setUser(null);
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    return <><Title title="Profil i bezpieczeństwo"/><div className="two-col"><div className="card"><h2>{user.displayName}</h2><p>{user.email}</p><span className="badge">{user.roles.join(", ")}</span><p>ID użytkownika: <code>{user.id}</code></p><div className="lab-note">Role i saldo są zarządzane wyłącznie na backendzie.</div></div><div className="card"><h2>Zmień hasło</h2><Message error={error}/><form onSubmit={submit}><Field label="Obecne hasło" name="currentPassword" type="password" required autoComplete="current-password"/><Field label="Nowe hasło" name="newPassword" type="password" required minLength={12} maxLength={128} autoComplete="new-password"/><small>Minimum 12 znaków, duża i mała litera, cyfra i znak specjalny. Zmiana unieważni sesje.</small><button disabled={busy}>Zmień hasło</button></form></div></div></>;
}
function AdminDashboard() {
    const u = useData("/admin/users"), a = useData("/admin/alerts"), l = useData("/admin/logs");
    return <><Title tag="CENTRUM BEZPIECZEŃSTWA" title="Panel administratora" text="Monitoruj zdarzenia, reaguj na alerty i kontroluj dostęp."/><State {...u}/><State {...a}/><div className="stats">{[["Użytkownicy", u.data?.length, "/admin/users"], ["Otwarte alerty", a.data?.filter(x => !x.resolved).length, "/admin/alerts"], ["Ostatnie zdarzenia", l.data?.length, "/admin/logs"]].map(([name, n, path]) => <Link className="card stat" to={path} key={name}><span>{name}</span><h2>{n ?? "—"}</h2><small>Otwórz panel <ChevronRight size={15}/></small></Link>)}</div><div className="info-strip"><ShieldCheck /><div><strong>Najmniejsze niezbędne uprawnienia</strong><p>Administrator zarządza dostępem i incydentami. Wykonywanie przelewów i zmiana salda klientów są niedostępne.</p></div></div><h2>Ostatnie alerty</h2>{a.data?.slice(0, 5).map(x => <div className="card alert" key={x.id}><Bell /><div><strong>{x.type}</strong><p>{x.description}</p><small>{date(x.createdAt)}</small></div><span className={"badge " + (x.severity === "CRITICAL" ? "critical" : "")}>{x.severity}</span></div>)}</>;
}
function AdminUsers() {
    const q = useData("/admin/users");
    const { user } = useContext(Auth);
    const [error, setError] = useState(""), [success, setSuccess] = useState(""), [busy, setBusy] = useState(false);
    async function change(id, kind, body) {
        setBusy(true);
        setError("");
        setSuccess("");
        try {
            const r = await api("/admin/users/" + id + "/" + kind, "PUT", body);
            setSuccess(r.message);
            q.refresh();
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    return <><Title title="Użytkownicy" text="Kontrolowane role i blokady. Zmiana roli unieważnia poprzednie sesje."/><Message error={error} success={success}/><State {...q}/><div className="card table-card"><table><thead><tr><th>Użytkownik</th><th>Rola</th><th>Status</th><th>Akcje</th></tr></thead><tbody>{q.data?.map(x => <tr key={x.id}><td><strong>{x.displayName}</strong><small>{x.email}</small><small>{x.id}</small></td><td><select aria-label={"Rola " + x.email} value={x.roles[0]} disabled={busy || x.id === user.id} onChange={e => change(x.id, "role", { role: e.target.value })}>{["CLIENT", "EMPLOYEE", "ADMIN"].map(r => <option key={r}>{r}</option>)}</select></td><td><span className="badge">{x.isBlocked ? "ZABLOKOWANY" : x.lockoutEnd && new Date(x.lockoutEnd) > new Date() ? "LOCKOUT" : "AKTYWNY"}</span></td><td><button className="secondary" disabled={busy || x.id === user.id || x.roles.includes("ADMIN")} onClick={() => change(x.id, "block", { blocked: !x.isBlocked && !x.lockoutEnd })}>{x.isBlocked || x.lockoutEnd ? "Odblokuj" : "Zablokuj"}</button></td></tr>)}</tbody></table></div></>;
}
function AuditPage({ alerts = false }) {
    const [filter, setFilter] = useState("");
    const q = useData("/admin/" + (alerts ? "alerts" : "logs") + filter);
    const [error, setError] = useState(""), [busy, setBusy] = useState(false);
    async function resolve(id) {
        setBusy(true);
        try {
            await api("/admin/alerts/" + id + "/resolve", "PUT");
            q.refresh();
        }
        catch (e) {
            setError(e.message);
        }
        finally {
            setBusy(false);
        }
    }
    function submit(e) {
        e.preventDefault();
        const p = new URLSearchParams();
        for (const [k, v] of new FormData(e.target)) {
            if (v)
                p.set(k, k === "from" || k === "to" ? new Date(v).toISOString() : v);
        }
        setFilter("?" + p);
    }
    return <><Title title={alerts ? "Alerty bezpieczeństwa" : "Logi bezpieczeństwa"} text="Wyniki ograniczone do 500 najnowszych rekordów."/><form className="card filters" onSubmit={submit}><Field label="ID użytkownika" name="userId"/><Field label="Typ zdarzenia" name="type"/><label>Severity<select name="severity"><option value="">Wszystkie</option>{["INFO", "WARNING", "CRITICAL"].map(x => <option key={x}>{x}</option>)}</select></label><Field label="Od" type="datetime-local" name="from"/><Field label="Do" type="datetime-local" name="to"/><button>Filtruj</button></form><Message error={error}/><State {...q}/><div className="card table-card"><table><thead><tr><th>Zdarzenie</th><th>Użytkownik / endpoint</th><th>Poziom</th><th>Data</th>{alerts && <th>Status</th>}</tr></thead><tbody>{q.data?.map(x => <tr key={x.id}><td><strong>{x.type || x.eventType}</strong><small>{x.description}</small></td><td><small>{x.userId || "Anonimowy"}</small><small>{x.endpoint} {x.ipAddress}</small></td><td><span className={"badge " + (x.severity === "CRITICAL" ? "critical" : "")}>{x.severity}</span></td><td>{date(x.createdAt || x.timestamp)}</td>{alerts && <td>{x.resolved ? <span className="green">Rozwiązany</span> : <button disabled={busy} className="secondary" onClick={() => resolve(x.id)}>Rozwiąż</button>}</td>}</tr>)}</tbody></table>{q.data?.length === 0 && <div className="empty">Brak wyników.</div>}</div></>;
}
function Employee() {
    const q = useData("/users");
    return <><Title tag="PANEL PRACOWNIKA" title="Klienci" text="Ograniczony katalog. Bez dostępu do sald i wykonywania przelewów."/><State {...q}/><div className="card table-card"><table><thead><tr><th>Nazwa</th><th>ID</th><th>Status</th></tr></thead><tbody>{q.data?.map(x => <tr key={x.id}><td>{x.displayName}</td><td><code>{x.id}</code></td><td>{x.isBlocked ? "Zablokowany" : "Aktywny"}</td></tr>)}</tbody></table></div></>;
}
createRoot(document.getElementById("root")).render(<BrowserRouter><App /></BrowserRouter>);
