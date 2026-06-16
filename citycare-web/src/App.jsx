import { useState, useEffect, useCallback, useRef } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

// ─── CONFIG ───────────────────────────────────────────────────────────────────
const API_BASE = "http://localhost:5158";

const getToken  = () => localStorage.getItem("access_token");
const getRefresh = () => localStorage.getItem("refresh_token");

async function apiFetch(path, opts = {}) {
  const token = getToken();
  const res = await fetch(`${API_BASE}${path}`, {
    ...opts,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(opts.headers || {}),
    },
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || `Erreur ${res.status}`);
  }
  if (res.status === 204) return null;
  return res.json();
}

// ─── DESIGN TOKENS ────────────────────────────────────────────────────────────
const C = {
  bg:          "#f5f0e8",
  white:       "#ffffff",
  orange:      "#f0a030",
  orangeHover: "#e09020",
  orangeLight: "#fef3e2",
  orangeBorder:"#f0a030",
  text:        "#1a1a1a",
  textMid:     "#555555",
  textLight:   "#888888",
  border:      "#e0d8cc",
  shadow:      "0 2px 12px rgba(0,0,0,0.08)",
  shadowHover: "0 4px 20px rgba(0,0,0,0.13)",
  radius:      "16px",
  radiusSm:    "10px",
  radiusXs:    "8px",
  green:       "#4caf50",
  red:         "#e53935",
  amber:       "#f0a030",
  blue:        "#1976d2",
};

const FONT = "'Inter', 'Segoe UI', sans-serif";

const INCIDENT_TYPES = ["Road","Lighting","Waste","Graffiti","Safety","Other"];
const TYPE_LABELS    = { Road:"Voirie", Lighting:"Éclairage", Waste:"Déchets", Graffiti:"Graffiti", Safety:"Sécurité", Other:"Autre" };
const TYPE_EMOJI     = { Road:"🛣️", Lighting:"💡", Waste:"🗑️", Graffiti:"🎨", Safety:"⚠️", Other:"📍" };
const STATUS_LABELS  = { reported:"Signalé", in_progress:"En cours", resolved:"Résolu" };
const STATUS_COLORS  = { reported: C.amber, in_progress: C.blue, resolved: C.green };

// ─── ATOMS ────────────────────────────────────────────────────────────────────

function Btn({ children, onClick, variant = "primary", style = {}, disabled, type = "button", fullWidth }) {
  const base = {
    display: "flex", alignItems: "center", justifyContent: "center", gap: "8px",
    padding: "13px 24px", borderRadius: C.radiusSm, border: "none",
    fontFamily: FONT, fontSize: "15px", fontWeight: 600,
    cursor: disabled ? "not-allowed" : "pointer",
    transition: "all .18s", opacity: disabled ? 0.6 : 1,
    width: fullWidth ? "100%" : "auto",
  };
  const styles = {
    primary: { background: C.orange, color: "#fff", boxShadow: "0 2px 8px rgba(240,160,48,0.3)" },
    outline: { background: C.white, color: C.text, border: `1.5px solid ${C.border}` },
    ghost:   { background: "transparent", color: C.textMid, border: `1px solid ${C.border}` },
    danger:  { background: C.red, color: "#fff" },
  };
  return (
    <button type={type} onClick={onClick} disabled={disabled}
      style={{ ...base, ...styles[variant], ...style }}
      onMouseEnter={e => { if (!disabled) e.currentTarget.style.filter = "brightness(0.93)"; }}
      onMouseLeave={e => { e.currentTarget.style.filter = ""; }}>
      {children}
    </button>
  );
}

function Input({ label, error, style = {}, ...props }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "6px", ...style }}>
      {label && <label style={{ fontSize: "13px", color: C.textMid, fontWeight: 500 }}>{label}</label>}
      <input {...props} style={{
        background: C.white, border: `1.5px solid ${error ? C.red : C.border}`,
        borderRadius: C.radiusXs, color: C.text, fontSize: "14px",
        padding: "11px 14px", outline: "none", fontFamily: FONT,
        width: "100%", boxSizing: "border-box", transition: "border-color .15s",
      }}
        onFocus={e => { e.target.style.borderColor = C.orange; }}
        onBlur={e => { e.target.style.borderColor = error ? C.red : C.border; }}
      />
      {error && <span style={{ fontSize: "12px", color: C.red }}>{error}</span>}
    </div>
  );
}

function Card({ children, style = {}, onClick }) {
  return (
    <div onClick={onClick} style={{
      background: C.white, borderRadius: C.radius,
      boxShadow: C.shadow, padding: "20px",
      cursor: onClick ? "pointer" : "default",
      transition: "box-shadow .18s",
      ...style,
    }}
      onMouseEnter={e => { if (onClick) e.currentTarget.style.boxShadow = C.shadowHover; }}
      onMouseLeave={e => { if (onClick) e.currentTarget.style.boxShadow = C.shadow; }}>
      {children}
    </div>
  );
}

function Badge({ status }) {
  const color = STATUS_COLORS[status] || C.textLight;
  return (
    <span style={{
      background: color + "18", color,
      border: `1px solid ${color}44`,
      borderRadius: "20px", padding: "3px 10px",
      fontSize: "12px", fontWeight: 600,
    }}>
      {STATUS_LABELS[status] || status}
    </span>
  );
}

function Toast({ message, type = "error", onClose }) {
  useEffect(() => { const t = setTimeout(onClose, 3500); return () => clearTimeout(t); }, [onClose]);
  return (
    <div style={{
      position: "fixed", bottom: "24px", right: "24px", zIndex: 9999,
      background: type === "error" ? C.red : C.green,
      color: "#fff", padding: "14px 20px", borderRadius: C.radiusSm,
      fontSize: "14px", fontWeight: 500, boxShadow: "0 8px 32px rgba(0,0,0,.15)",
      maxWidth: "360px", fontFamily: FONT,
    }}>
      {message}
    </div>
  );
}

function Spinner() {
  return (
    <div style={{ display: "flex", justifyContent: "center", padding: "48px" }}>
      <div style={{
        width: "32px", height: "32px",
        border: `3px solid ${C.border}`,
        borderTop: `3px solid ${C.orange}`,
        borderRadius: "50%", animation: "spin .7s linear infinite",
      }} />
    </div>
  );
}

// Centre par défaut (Lyon) quand aucun marqueur n'est disponible
const DEFAULT_CENTER = [45.764, 4.8357];
const DEFAULT_ZOOM = 12;

// Pin coloré (même visuel que l'ancienne carte), réutilise STATUS_COLORS
function makePinIcon(status) {
  const color = STATUS_COLORS[status] || C.orange;
  return L.divIcon({
    className: "",
    html: `<svg width="28" height="36" viewBox="0 0 28 36" style="filter:drop-shadow(0 2px 4px rgba(0,0,0,.3))">
      <path d="M14 0C6.27 0 0 6.27 0 14c0 9.33 14 22 14 22S28 23.33 28 14C28 6.27 21.73 0 14 0z" fill="${color}"/>
      <circle cx="14" cy="14" r="6" fill="white" opacity="0.9"/>
    </svg>`,
    iconSize: [28, 36],
    iconAnchor: [14, 36],   // la pointe du pin
    popupAnchor: [0, -36],
  });
}

// Carte Leaflet + OpenStreetMap (aucune clé API requise).
// Interface compatible avec l'ancien FakeMap : { markers, onMarkerClick, height }.
// Bonus : onMapClick(lat, lng) pour laisser l'utilisateur poser un point.
function LeafletMap({
  markers = [],
  onMarkerClick,
  onMapClick,
  height = "200px",
  center = DEFAULT_CENTER,
  zoom = DEFAULT_ZOOM,
}) {
  const containerRef = useRef(null);
  const mapRef = useRef(null);
  const layerRef = useRef(null);

  // Initialisation de la carte (une seule fois)
  useEffect(() => {
    if (mapRef.current) return;
    const map = L.map(containerRef.current).setView(center, zoom);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: "&copy; OpenStreetMap",
    }).addTo(map);
    layerRef.current = L.layerGroup().addTo(map);
    mapRef.current = map;
    // Leaflet calcule mal sa taille si le conteneur s'affiche après coup
    setTimeout(() => map.invalidateSize(), 0);
    return () => { map.remove(); mapRef.current = null; };
  }, []);

  // Clic sur la carte -> remonte les coordonnées au parent
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;
    const handler = (e) => onMapClick && onMapClick(e.latlng.lat, e.latlng.lng);
    map.on("click", handler);
    return () => map.off("click", handler);
  }, [onMapClick]);

  // (Re)dessine les marqueurs quand la liste change
  useEffect(() => {
    const map = mapRef.current, group = layerRef.current;
    if (!map || !group) return;
    group.clearLayers();

    const valid = markers.filter(m => isFinite(m.latitude) && isFinite(m.longitude));
    valid.forEach(m => {
      const marker = L.marker([m.latitude, m.longitude], { icon: makePinIcon(m.status) });
      if (onMarkerClick) marker.on("click", () => onMarkerClick(m));
      marker.addTo(group);
    });

    if (valid.length === 1) {
      map.setView([valid[0].latitude, valid[0].longitude], 15);
    } else if (valid.length > 1) {
      map.fitBounds(L.latLngBounds(valid.map(m => [m.latitude, m.longitude])).pad(0.2));
    }
  }, [markers, onMarkerClick]);

  return (
    <div ref={containerRef} style={{
      height, borderRadius: C.radiusSm, overflow: "hidden",
      border: `1px solid ${C.border}`,
    }} />
  );
}

// ─── AUTH PAGES ───────────────────────────────────────────────────────────────

function LoginPage({ onLogin, onGoRegister, toast }) {
  const [form, setForm] = useState({ username: "", password: "" });
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState({});

  const submit = async () => {
    const e = {};
    if (!form.username) e.username = "Requis";
    if (!form.password) e.password = "Requis";
    if (Object.keys(e).length) { setErrors(e); return; }
    setLoading(true);
    try {
      const data = await apiFetch("/auth/login", {
        method: "POST", body: JSON.stringify(form),
      });
      localStorage.setItem("access_token", data.accessToken);
      localStorage.setItem("refresh_token", data.refreshToken);
      onLogin();
    } catch (err) {
      toast(err.message, "error");
    } finally { setLoading(false); }
  };

  return (
    <AuthShell>
      {/* Logo */}
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", marginBottom: "28px" }}>
        <div style={{
          width: "72px", height: "72px", background: C.orangeLight,
          borderRadius: "16px", display: "flex", alignItems: "center", justifyContent: "center",
          marginBottom: "16px", border: `2px solid ${C.orange}33`,
        }}>
          <span style={{ fontSize: "32px" }}>🏙️</span>
        </div>
        <h1 style={{ margin: 0, fontSize: "26px", fontWeight: 700, color: C.text }}>CityCare+</h1>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <Input label="Email" placeholder="exemple@mail.com" value={form.username} error={errors.username}
          onChange={e => setForm({ ...form, username: e.target.value })}
          onKeyDown={e => e.key === "Enter" && submit()} />
        <Input label="Mot de passe" type="password" placeholder="••••••••" value={form.password} error={errors.password}
          onChange={e => setForm({ ...form, password: e.target.value })}
          onKeyDown={e => e.key === "Enter" && submit()} />

        <Btn onClick={submit} disabled={loading} fullWidth style={{ marginTop: "8px" }}>
          {loading ? "Connexion…" : "Se connecter"}
        </Btn>
        <Btn onClick={onGoRegister} variant="outline" fullWidth>
          Créer un compte
        </Btn>
      </div>
    </AuthShell>
  );
}

function RegisterPage({ onGoLogin, toast }) {
  const [form, setForm] = useState({ email: "", username: "", firstName: "", lastName: "", password: "" });
  const [loading, setLoading] = useState(false);
  const [errors, setErrors] = useState({});

  const validate = () => {
    const e = {};
    if (!form.email || !/\S+@\S+\.\S+/.test(form.email)) e.email = "Email invalide";
    if (!form.username || form.username.length < 3) e.username = "Min. 3 caractères";
    if (!form.firstName) e.firstName = "Requis";
    if (!form.lastName) e.lastName = "Requis";
    if (!form.password || form.password.length < 8) e.password = "Min. 8 caractères";
    return e;
  };

  const submit = async () => {
    const e = validate();
    if (Object.keys(e).length) { setErrors(e); return; }
    setLoading(true);
    try {
      await apiFetch("/auth/register", { method: "POST", body: JSON.stringify(form) });
      toast("Compte créé ! Connectez-vous.", "success");
      onGoLogin();
    } catch (err) {
      toast(err.message, "error");
    } finally { setLoading(false); }
  };

  const f = k => ({ value: form[k], error: errors[k], onChange: e => setForm({ ...form, [k]: e.target.value }) });

  return (
    <AuthShell title="Créer un compte">
      <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "14px" }}>
          <Input label="Prénom" {...f("firstName")} />
          <Input label="Nom" {...f("lastName")} />
        </div>
        <Input label="Email" type="email" {...f("email")} />
        <Input label="Nom d'utilisateur" {...f("username")} />
        <Input label="Mot de passe" type="password" {...f("password")} />
        <Btn onClick={submit} disabled={loading} fullWidth style={{ marginTop: "4px" }}>
          {loading ? "Création…" : "Créer mon compte"}
        </Btn>
        <Btn onClick={onGoLogin} variant="outline" fullWidth>Déjà un compte ? Se connecter</Btn>
      </div>
    </AuthShell>
  );
}

function AuthShell({ children, title }) {
  return (
    <div style={{
      minHeight: "100vh", background: C.bg,
      display: "flex", alignItems: "center", justifyContent: "center",
      padding: "24px", fontFamily: FONT,
    }}>
      <div style={{ width: "100%", maxWidth: "380px" }}>
        {title && (
          <h2 style={{ textAlign: "center", margin: "0 0 24px", fontSize: "22px", fontWeight: 700, color: C.text }}>
            {title}
          </h2>
        )}
        <Card style={{ padding: "32px" }}>{children}</Card>
      </div>
    </div>
  );
}

// ─── CARTE (tâches 4 & 5) ────────────────────────────────────────────────────

function MapPage({ navigate, toast, onLogout }) {
  const [incidents, setIncidents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState(null);
  const [filterOpen, setFilterOpen] = useState(false);
  const [filters, setFilters] = useState({ status: "", type: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ pageSize: 100 });
      if (filters.status) params.set("status", filters.status);
      if (filters.type)   params.set("type", filters.type);
      const data = await apiFetch(`/incidents?${params}`);
      setIncidents(data.data || []);
    } catch (err) { toast(err.message, "error"); }
    finally { setLoading(false); }
  }, [filters]);

  useEffect(() => { load(); }, [load]);

  const markers = incidents.map(inc => ({
    id: inc.id, status: inc.status,
    latitude: inc.latitude, longitude: inc.longitude,
    ...inc,
  }));

  return (
    <AppLayout title="Carte" navigate={navigate} onLogout={onLogout}>
      <Card style={{ padding: "20px" }}>
        {/* Carte */}
        <LeafletMap markers={markers} height="220px"
          onMarkerClick={m => setSelected(selected?.id === m.id ? null : m)} />

        {/* Bouton Filtrer */}
        <button onClick={() => setFilterOpen(!filterOpen)} style={{
          width: "100%", marginTop: "14px", padding: "12px",
          background: C.white, border: `1.5px solid ${C.border}`,
          borderRadius: C.radiusXs, cursor: "pointer", fontFamily: FONT,
          fontSize: "14px", color: C.textMid, display: "flex",
          alignItems: "center", justifyContent: "center", gap: "8px",
          transition: "border-color .15s",
        }}>
          🔍 Filtrer
        </button>

        {/* Panneau filtres */}
        {filterOpen && (
          <div style={{ marginTop: "12px", display: "flex", gap: "10px", flexWrap: "wrap" }}>
            {[
              { label: "Statut", key: "status", options: [["","Tous les statuts"],["reported","Signalé"],["in_progress","En cours"],["resolved","Résolu"]] },
              { label: "Type",   key: "type",   options: [["","Tous les types"], ...INCIDENT_TYPES.map(t => [t, TYPE_LABELS[t]])] },
            ].map(({ label, key, options }) => (
              <select key={key} value={filters[key]}
                onChange={e => setFilters({ ...filters, [key]: e.target.value })}
                style={{
                  flex: 1, minWidth: "140px", background: C.white,
                  border: `1.5px solid ${C.border}`, borderRadius: C.radiusXs,
                  color: C.text, padding: "9px 12px", fontFamily: FONT,
                  fontSize: "13px", cursor: "pointer", outline: "none",
                }}>
                {options.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            ))}
          </div>
        )}

        {/* Détails incident sélectionné (popup marker — tâche 5) */}
        {selected && (
          <div style={{ marginTop: "16px", borderTop: `1px solid ${C.border}`, paddingTop: "16px" }}>
            <p style={{ margin: "0 0 8px", fontWeight: 700, fontSize: "14px", color: C.text }}>
              Détails de l'incident
            </p>
            <p style={{ margin: "4px 0", fontSize: "13px", color: C.textMid }}>
              <strong>Type :</strong> {TYPE_LABELS[selected.type] || selected.type}
            </p>
            <p style={{ margin: "4px 0", fontSize: "13px", color: C.textMid }}>
              <strong>Statut :</strong> {STATUS_LABELS[selected.status] || selected.status}
            </p>
            <p style={{ margin: "4px 0", fontSize: "13px", color: C.textMid }}>
              <strong>Localisation :</strong> {selected.addressLabel || "—"}
            </p>
            <button onClick={() => navigate("incident-detail", selected.id)}
              style={{
                marginTop: "10px", background: "none", border: "none",
                color: C.orange, fontWeight: 600, cursor: "pointer",
                fontFamily: FONT, fontSize: "13px", padding: 0,
              }}>
              Voir le détail complet →
            </button>
          </div>
        )}
      </Card>

      {loading && <div style={{ textAlign: "center", marginTop: "16px", color: C.textLight, fontSize: "13px" }}>Chargement…</div>}
    </AppLayout>
  );
}

// ─── DÉTAIL INCIDENT (tâche 6) ────────────────────────────────────────────────

function IncidentDetailPage({ incidentId, navigate, toast, userRole, onLogout }) {
  const [incident, setIncident] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiFetch(`/incidents/${incidentId}`)
      .then(setIncident)
      .catch(err => toast(err.message, "error"))
      .finally(() => setLoading(false));
  }, [incidentId]);

  const updateStatus = async (status) => {
    try {
      await apiFetch(`/incidents/${incidentId}/status`, {
        method: "PATCH", body: JSON.stringify({ status }),
      });
      toast("Statut mis à jour", "success");
      setIncident(prev => ({ ...prev, status }));
    } catch (err) { toast(err.message, "error"); }
  };

  if (loading) return <AppLayout title="Détail" navigate={navigate} onLogout={onLogout} back={() => navigate("map")}><Spinner /></AppLayout>;

  return (
    <AppLayout title="Détail de l'incident" navigate={navigate} onLogout={onLogout} back={() => navigate("map")}>
      {incident && (
        <>
          <LeafletMap markers={[{ id: incident.id, status: incident.status, latitude: incident.latitude, longitude: incident.longitude }]} height="180px" />
          <Card style={{ marginTop: "16px" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "16px" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                <span style={{ fontSize: "24px" }}>{TYPE_EMOJI[incident.type]}</span>
                <div>
                  <h2 style={{ margin: 0, fontSize: "18px", fontWeight: 700, color: C.text }}>
                    {TYPE_LABELS[incident.type] || incident.type}
                  </h2>
                  <p style={{ margin: "2px 0 0", fontSize: "12px", color: C.textLight }}>
                    par {incident.authorDisplayName || "Anonyme"}
                  </p>
                </div>
              </div>
              <Badge status={incident.status} />
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
              {[
                ["📍", "Localisation", incident.addressLabel],
                ["📝", "Description", incident.description],
                ["📅", "Signalé le", new Date(incident.createdAt).toLocaleDateString("fr-FR", { day:"2-digit", month:"long", year:"numeric" })],
              ].map(([icon, label, value]) => (
                <div key={label} style={{ display: "flex", gap: "8px" }}>
                  <span style={{ fontSize: "14px", minWidth: "20px" }}>{icon}</span>
                  <div>
                    <span style={{ fontSize: "12px", color: C.textLight }}>{label}</span>
                    <p style={{ margin: "2px 0 0", fontSize: "14px", color: C.text }}>{value || "—"}</p>
                  </div>
                </div>
              ))}
            </div>

            {(userRole === "agent" || userRole === "admin") && (
              <div style={{ marginTop: "20px", paddingTop: "16px", borderTop: `1px solid ${C.border}` }}>
                <p style={{ margin: "0 0 10px", fontSize: "12px", color: C.textLight, fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.5px" }}>
                  Changer le statut
                </p>
                <div style={{ display: "flex", gap: "10px" }}>
                  {incident.status === "reported" && (
                    <Btn onClick={() => updateStatus("in_progress")} style={{ fontSize: "13px", padding: "8px 16px" }}>
                      → En cours
                    </Btn>
                  )}
                  {incident.status === "in_progress" && (
                    <Btn onClick={() => updateStatus("resolved")} style={{ fontSize: "13px", padding: "8px 16px", background: C.green }}>
                      ✓ Résolu
                    </Btn>
                  )}
                </div>
              </div>
            )}
          </Card>
        </>
      )}
    </AppLayout>
  );
}

// ─── FORMULAIRE SIGNALEMENT (tâche 7) ─────────────────────────────────────────

function CreateIncidentPage({ navigate, toast, onLogout }) {
  const [form, setForm] = useState({ type: "", description: "", latitude: "", longitude: "" });
  const [loading, setLoading] = useState(false);
  const [geoLoading, setGeoLoading] = useState(false);
  const [errors, setErrors] = useState({});
  const [addressLabel, setAddressLabel] = useState("");

  const validate = () => {
    const e = {};
    if (!form.description || form.description.length < 10) e.description = "Min. 10 caractères";
    if (!form.type) e.type = "Sélectionnez une catégorie";
    if (!form.latitude || isNaN(parseFloat(form.latitude))) e.latitude = "Latitude invalide";
    if (!form.longitude || isNaN(parseFloat(form.longitude))) e.longitude = "Longitude invalide";
    return e;
  };

  const geolocate = () => {
    setGeoLoading(true);
    navigator.geolocation.getCurrentPosition(
      pos => {
        setForm({ ...form, latitude: pos.coords.latitude.toFixed(6), longitude: pos.coords.longitude.toFixed(6) });
        setAddressLabel("Position actuelle");
        setGeoLoading(false);
      },
      () => { toast("Géolocalisation refusée", "error"); setGeoLoading(false); }
    );
  };

  const submit = async () => {
    const e = validate();
    if (Object.keys(e).length) { setErrors(e); return; }
    setLoading(true);
    try {
      await apiFetch("/incidents", {
        method: "POST",
        body: JSON.stringify({
          type: form.type, description: form.description,
          latitude: parseFloat(form.latitude), longitude: parseFloat(form.longitude),
        }),
      });
      toast("Signalement envoyé !", "success");
      navigate("my-incidents");
    } catch (err) { toast(err.message, "error"); }
    finally { setLoading(false); }
  };

  return (
    <AppLayout title="Signaler un incident" navigate={navigate} onLogout={onLogout} back={() => navigate("map")}>
      <Card>
        {/* Mini carte — clic pour poser le point */}
        <LeafletMap
          height="180px"
          markers={form.latitude && form.longitude
            ? [{ id: "new", status: "reported",
                 latitude: parseFloat(form.latitude),
                 longitude: parseFloat(form.longitude) }]
            : []}
          onMapClick={(lat, lng) => setForm(f => ({
            ...f, latitude: lat.toFixed(6), longitude: lng.toFixed(6),
          }))} />

        <div style={{ display: "flex", flexDirection: "column", gap: "16px", marginTop: "16px" }}>
          {/* Description */}
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", color: C.textMid, fontWeight: 500 }}>Description</label>
            <textarea value={form.description}
              onChange={e => setForm({ ...form, description: e.target.value })}
              rows={4} placeholder="Décrivez brièvement l'incident..."
              style={{
                background: C.white, border: `1.5px solid ${errors.description ? C.red : C.border}`,
                borderRadius: C.radiusXs, color: C.text, fontSize: "14px",
                padding: "11px 14px", outline: "none", fontFamily: FONT,
                resize: "vertical", boxSizing: "border-box", width: "100%",
              }}
              onFocus={e => { e.target.style.borderColor = C.orange; }}
              onBlur={e => { e.target.style.borderColor = errors.description ? C.red : C.border; }}
            />
            {errors.description && <span style={{ fontSize: "12px", color: C.red }}>{errors.description}</span>}
          </div>

          {/* Catégorie */}
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", color: C.textMid, fontWeight: 500 }}>Catégorie</label>
            <select value={form.type} onChange={e => setForm({ ...form, type: e.target.value })}
              style={{
                background: C.white, border: `1.5px solid ${errors.type ? C.red : C.border}`,
                borderRadius: C.radiusXs, color: form.type ? C.text : C.textLight,
                padding: "11px 14px", fontFamily: FONT, fontSize: "14px",
                outline: "none", width: "100%", cursor: "pointer",
              }}>
              <option value="">Sélectionner une catégorie</option>
              {INCIDENT_TYPES.map(t => <option key={t} value={t}>{TYPE_EMOJI[t]} {TYPE_LABELS[t]}</option>)}
            </select>
            {errors.type && <span style={{ fontSize: "12px", color: C.red }}>{errors.type}</span>}
          </div>

          {/* Localisation */}
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", color: C.textMid, fontWeight: 500 }}>Localisation</label>
            <div style={{ display: "flex", gap: "8px" }}>
              <input value={addressLabel || (form.latitude ? `${form.latitude}, ${form.longitude}` : "")}
                readOnly placeholder="Cliquez sur Ma position..."
                style={{
                  flex: 1, background: C.white, border: `1.5px solid ${C.border}`,
                  borderRadius: C.radiusXs, color: C.text, fontSize: "14px",
                  padding: "11px 14px", outline: "none", fontFamily: FONT,
                }}
              />
              <Btn onClick={geolocate} disabled={geoLoading} variant="outline"
                style={{ flexShrink: 0, padding: "11px 14px", fontSize: "13px" }}>
                {geoLoading ? "…" : "📍 Ma position"}
              </Btn>
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "8px" }}>
              <Input placeholder="Latitude" value={form.latitude} error={errors.latitude}
                onChange={e => setForm({ ...form, latitude: e.target.value })} />
              <Input placeholder="Longitude" value={form.longitude} error={errors.longitude}
                onChange={e => setForm({ ...form, longitude: e.target.value })} />
            </div>
          </div>

          <Btn onClick={submit} disabled={loading} fullWidth style={{ marginTop: "4px" }}>
            {loading ? "Envoi…" : "Envoyer le signalement"}
          </Btn>
        </div>
      </Card>
    </AppLayout>
  );
}

// ─── MES SIGNALEMENTS (tâche 8) ───────────────────────────────────────────────

function MyIncidentsPage({ navigate, toast, onLogout }) {
  const [incidents, setIncidents] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiFetch("/users/me/incidents")
      .then(d => setIncidents(d.data || []))
      .catch(err => toast(err.message, "error"))
      .finally(() => setLoading(false));
  }, []);

  return (
    <AppLayout title="Mes signalements" navigate={navigate} onLogout={onLogout}>
      {loading ? <Spinner /> : incidents.length === 0 ? (
        <Card style={{ textAlign: "center", padding: "48px 24px" }}>
          <div style={{ fontSize: "40px", marginBottom: "12px" }}>📭</div>
          <p style={{ color: C.textMid, margin: "0 0 20px", fontSize: "15px" }}>Aucun signalement pour l'instant</p>
          <Btn onClick={() => navigate("create-incident")}>Créer un signalement</Btn>
        </Card>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
          {incidents.map(inc => (
            <Card key={inc.id} onClick={() => navigate("incident-detail", inc.id)}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "12px" }}>
                  <div style={{
                    width: "42px", height: "42px", borderRadius: "12px",
                    background: C.orangeLight, display: "flex", alignItems: "center", justifyContent: "center",
                    fontSize: "20px", flexShrink: 0,
                  }}>
                    {TYPE_EMOJI[inc.type] || "📍"}
                  </div>
                  <div>
                    <p style={{ margin: 0, fontWeight: 600, fontSize: "14px", color: C.text }}>
                      {TYPE_LABELS[inc.type] || inc.type}
                    </p>
                    <p style={{ margin: "2px 0 0", fontSize: "12px", color: C.textLight }}>
                      {inc.address_label || "—"}
                    </p>
                  </div>
                </div>
                <div style={{ textAlign: "right", display: "flex", flexDirection: "column", alignItems: "flex-end", gap: "6px" }}>
                  <Badge status={inc.status} />
                  <span style={{ fontSize: "11px", color: C.textLight }}>
                    {new Date(inc.created_at).toLocaleDateString("fr-FR")}
                  </span>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </AppLayout>
  );
}

// ─── PROFIL (tâche 9) ─────────────────────────────────────────────────────────

function ProfilePage({ navigate, toast, onLogout }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    apiFetch("/users/me")
      .then(setUser)
      .catch(err => toast(err.message, "error"))
      .finally(() => setLoading(false));
  }, []);

  return (
    <AppLayout title="Mon profil" navigate={navigate} onLogout={onLogout}>
      {loading ? <Spinner /> : user && (
        <div style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
          <Card style={{ textAlign: "center", padding: "28px" }}>
            <div style={{
              width: "72px", height: "72px", borderRadius: "50%",
              background: `linear-gradient(135deg, ${C.orange}, #ffcc70)`,
              display: "flex", alignItems: "center", justifyContent: "center",
              fontSize: "28px", fontWeight: 700, color: "#fff",
              margin: "0 auto 14px",
            }}>
              {(user.display_name || "?")[0].toUpperCase()}
            </div>
            <h2 style={{ margin: "0 0 4px", fontSize: "20px", fontWeight: 700, color: C.text }}>
              {user.display_name || "—"}
            </h2>
            <p style={{ margin: "0 0 10px", fontSize: "14px", color: C.textLight }}>{user.email}</p>
            <span style={{
              display: "inline-block", background: C.orangeLight, color: C.orange,
              border: `1px solid ${C.orange}44`, borderRadius: "20px",
              padding: "3px 12px", fontSize: "12px", fontWeight: 600,
            }}>
              {user.role === "citizen" ? "Citoyen" : user.role === "agent" ? "Agent" : "Administrateur"}
            </span>
          </Card>

          <Card>
            {[
              ["📧", "Email", user.email],
              ["📅", "Membre depuis", new Date(user.created_at).toLocaleDateString("fr-FR", { day:"2-digit", month:"long", year:"numeric" })],
              ["🔑", "Rôle", user.role === "citizen" ? "Citoyen" : user.role === "agent" ? "Agent" : "Administrateur"],
            ].map(([icon, label, value]) => (
              <div key={label} style={{
                display: "flex", alignItems: "center", gap: "12px",
                padding: "12px 0", borderBottom: `1px solid ${C.border}`,
              }}>
                <span style={{ fontSize: "18px", width: "24px", textAlign: "center" }}>{icon}</span>
                <div>
                  <p style={{ margin: 0, fontSize: "11px", color: C.textLight }}>{label}</p>
                  <p style={{ margin: "2px 0 0", fontSize: "14px", color: C.text, fontWeight: 500 }}>{value}</p>
                </div>
              </div>
            ))}
          </Card>

          <Btn onClick={onLogout} variant="outline" fullWidth
            style={{ color: C.red, borderColor: C.red + "66" }}>
            🚪 Se déconnecter
          </Btn>
        </div>
      )}
    </AppLayout>
  );
}

// ─── ADMIN DASHBOARD (tâche 11 — suppression + gestion) ──────────────────────

function AdminPage({ navigate, toast, onLogout }) {
  const [incidents, setIncidents] = useState([]);
  const [users, setUsers] = useState([]);
  const [stats, setStats] = useState({ total: 0, resolved: 0 });
  const [loading, setLoading] = useState(true);
  const [deleting, setDeleting] = useState(null);

  useEffect(() => {
    Promise.all([
      apiFetch("/incidents?pageSize=50"),
      apiFetch("/users/me").catch(() => null),
    ]).then(([inc]) => {
      const list = inc.data || [];
      setIncidents(list);
      setStats({
        total: inc.pagination?.total_count || list.length,
        resolved: list.filter(i => i.status === "resolved").length,
      });
    }).catch(err => toast(err.message, "error"))
      .finally(() => setLoading(false));
  }, []);

  const deleteIncident = async (id) => {
    if (!window.confirm("Supprimer cet incident définitivement ?")) return;
    setDeleting(id);
    try {
      await apiFetch(`/incidents/${id}`, { method: "DELETE" });
      toast("Incident supprimé", "success");
      setIncidents(prev => prev.filter(i => i.id !== id));
    } catch (err) { toast(err.message, "error"); }
    finally { setDeleting(null); }
  };

  return (
    <AppLayout title="Tableau de bord" navigate={navigate} onLogout={onLogout}>
      {/* Stats */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "12px", marginBottom: "16px" }}>
        {[
          { value: stats.total, label: "Incidents recensés" },
          { value: stats.resolved, label: "Résolus aujourd'hui" },
          { value: incidents.filter(i => i.status !== "resolved").length, label: "En cours" },
        ].map(({ value, label }) => (
          <Card key={label} style={{ textAlign: "center", padding: "16px" }}>
            <p style={{ margin: 0, fontSize: "28px", fontWeight: 700, color: C.orange }}>{value}</p>
            <p style={{ margin: "4px 0 0", fontSize: "11px", color: C.textLight }}>{label}</p>
          </Card>
        ))}
      </div>

      {/* Gestion incidents */}
      <Card>
        <h3 style={{ margin: "0 0 16px", fontSize: "16px", fontWeight: 700, color: C.text }}>
          Gestion des incidents
        </h3>
        {loading ? <Spinner /> : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", fontFamily: FONT }}>
              <thead>
                <tr style={{ background: C.orange }}>
                  {["Type", "Statut", "Localisation", "Actions"].map(h => (
                    <th key={h} style={{ padding: "10px 12px", textAlign: "left", color: "#fff", fontWeight: 600, fontSize: "13px" }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {incidents.map((inc, i) => (
                  <tr key={inc.id} style={{ background: i % 2 === 0 ? C.white : "#fafaf8" }}>
                    <td style={{ padding: "10px 12px", color: C.text }}>
                      {TYPE_EMOJI[inc.type]} {TYPE_LABELS[inc.type] || inc.type}
                    </td>
                    <td style={{ padding: "10px 12px" }}><Badge status={inc.status} /></td>
                    <td style={{ padding: "10px 12px", color: C.textMid, maxWidth: "160px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {inc.addressLabel || "—"}
                    </td>
                    <td style={{ padding: "10px 12px" }}>
                      <div style={{ display: "flex", gap: "6px" }}>
                        <button onClick={() => navigate("incident-detail", inc.id)}
                          style={{ background: C.orange, color: "#fff", border: "none", borderRadius: "6px", padding: "5px 10px", fontSize: "12px", cursor: "pointer", fontFamily: FONT, fontWeight: 500 }}>
                          Modifier
                        </button>
                        <button onClick={() => deleteIncident(inc.id)} disabled={deleting === inc.id}
                          style={{ background: "#ffebee", color: C.red, border: `1px solid ${C.red}33`, borderRadius: "6px", padding: "5px 10px", fontSize: "12px", cursor: "pointer", fontFamily: FONT, fontWeight: 500 }}>
                          {deleting === inc.id ? "…" : "Supprimer"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </AppLayout>
  );
}

// ─── APP LAYOUT ───────────────────────────────────────────────────────────────

function AppLayout({ title, children, navigate, back, onLogout }) {
  const navItems = [
    { id: "map",             icon: "🗺️",  label: "Carte" },
    { id: "create-incident", icon: "➕",  label: "Signaler" },
    { id: "my-incidents",    icon: "📋",  label: "Mes signalements" },
    { id: "profile",         icon: "👤",  label: "Profil" },
  ];

  return (
    <div style={{ minHeight: "100vh", background: C.bg, fontFamily: FONT, color: C.text }}>
      {/* Header */}
      <header style={{
        background: C.white, borderBottom: `1px solid ${C.border}`,
        padding: "0 20px", height: "56px",
        display: "flex", alignItems: "center", gap: "10px",
        position: "sticky", top: 0, zIndex: 100,
        boxShadow: "0 1px 8px rgba(0,0,0,0.06)",
      }}>
        {back && (
          <button onClick={back} style={{
            background: "none", border: "none", color: C.textMid,
            cursor: "pointer", fontSize: "18px", padding: "4px", display: "flex",
          }}>←</button>
        )}
        <span style={{ fontSize: "20px" }}>🏙️</span>
        <span style={{ fontWeight: 700, fontSize: "16px", color: C.text }}>CityCare+</span>
        {title && <span style={{ color: C.textLight, fontSize: "14px" }}>/ {title}</span>}
      </header>

      {/* Content */}
      <main style={{ maxWidth: "600px", margin: "0 auto", padding: "20px 16px 100px" }}>
        {children}
      </main>

      {/* Bottom nav */}
      <nav style={{
        position: "fixed", bottom: 0, left: 0, right: 0,
        background: C.white, borderTop: `1px solid ${C.border}`,
        display: "flex", justifyContent: "space-around",
        padding: "8px 0 12px", zIndex: 100,
        boxShadow: "0 -2px 12px rgba(0,0,0,0.06)",
      }}>
        {navItems.map(item => (
          <button key={item.id} onClick={() => navigate(item.id)}
            style={{
              background: "none", border: "none", cursor: "pointer",
              display: "flex", flexDirection: "column", alignItems: "center", gap: "3px",
              color: C.textMid, fontSize: "11px", fontFamily: FONT,
              padding: "4px 16px", transition: "color .15s",
            }}
            onMouseEnter={e => { e.currentTarget.style.color = C.orange; }}
            onMouseLeave={e => { e.currentTarget.style.color = C.textMid; }}>
            <span style={{ fontSize: "20px" }}>{item.icon}</span>
            {item.label}
          </button>
        ))}
        {onLogout && (
          <button onClick={onLogout}
            style={{
              background: "none", border: "none", cursor: "pointer",
              display: "flex", flexDirection: "column", alignItems: "center", gap: "3px",
              color: C.red, fontSize: "11px", fontFamily: FONT, padding: "4px 16px",
            }}>
            <span style={{ fontSize: "20px" }}>🚪</span>
            Déconnexion
          </button>
        )}
      </nav>
    </div>
  );
}

// ─── APP ROOT ─────────────────────────────────────────────────────────────────

export default function App() {
  const [page, setPage] = useState("login");
  const [pageParam, setPageParam] = useState(null);
  const [isAuth, setIsAuth] = useState(!!getToken());
  const [userRole, setUserRole] = useState("citizen");
  const [toastData, setToastData] = useState(null);

  const toast = useCallback((message, type = "error") => {
    setToastData({ message, type, key: Date.now() });
  }, []);

  const navigate = useCallback((p, param = null) => {
    setPage(p); setPageParam(param);
  }, []);

  const handleLogin = useCallback(async () => {
    setIsAuth(true);
    try {
      const me = await apiFetch("/users/me");
      setUserRole(me.role || "citizen");
    } catch {}
    setPage("map");
  }, []);

  const handleLogout = useCallback(async () => {
    const refresh = getRefresh();
    if (refresh) {
      try { await apiFetch("/auth/logout", { method: "POST", body: JSON.stringify({ RefreshToken: refresh }) }); } catch {}
    }
    localStorage.removeItem("access_token");
    localStorage.removeItem("refresh_token");
    setIsAuth(false);
    setPage("login");
  }, []);

  useEffect(() => {
    if (isAuth && page === "login") setPage("map");
    if (!isAuth && !["login","register"].includes(page)) setPage("login");
  }, [isAuth, page]);

  const props = { navigate, toast, onLogout: handleLogout };

  const renderPage = () => {
    if (!isAuth) {
      if (page === "register") return <RegisterPage onGoLogin={() => setPage("login")} toast={toast} />;
      return <LoginPage onLogin={handleLogin} onGoRegister={() => setPage("register")} toast={toast} />;
    }
    switch (page) {
      case "map":             return <MapPage {...props} />;
      case "incident-detail": return <IncidentDetailPage {...props} incidentId={pageParam} userRole={userRole} />;
      case "create-incident": return <CreateIncidentPage {...props} />;
      case "my-incidents":    return <MyIncidentsPage {...props} />;
      case "profile":         return <ProfilePage {...props} />;
      case "admin":           return <AdminPage {...props} />;
      default:                return <MapPage {...props} />;
    }
  };

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { background: ${C.bg}; }
        @keyframes spin { to { transform: rotate(360deg); } }
        input::placeholder, textarea::placeholder { color: ${C.textLight}; }
        select option { background: ${C.white}; color: ${C.text}; }
        ::-webkit-scrollbar { width: 5px; }
        ::-webkit-scrollbar-track { background: ${C.bg}; }
        ::-webkit-scrollbar-thumb { background: ${C.border}; border-radius: 3px; }
      `}</style>
      {renderPage()}
      {toastData && (
        <Toast key={toastData.key} message={toastData.message} type={toastData.type} onClose={() => setToastData(null)} />
      )}
    </>
  );
}
