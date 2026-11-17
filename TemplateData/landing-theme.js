/* MATU FArM — which look the loading page wears. The rule is the game's own (Core/LandingTheme.cs); the calendar it reads is
   TemplateData/landing.js, written by every web build from Core/MoonCalendar.cs and Core/FestivalSys.cs (Editor/LandingWeb.cs), so
   the page and the game can never disagree about when Hội Trăng Rằm or the weekend festival is on. No landing.js: the everyday page.

     moon      Hội Trăng Rằm shows: from its opening to the end of its claim day (the night page: moon, lanterns, the event's ribbon)
     festival  the weekend's Lễ hội Làng Mây is open: Saturday 00:00 → Monday 00:00, Việt Nam time (the day page with balloons)
     default   anything else — the page exactly as it always was

   For a look at the page: ?theme=moon|festival|default pins a look, ?at=<UTC ms> picks the moment. */
(function (root) {
  "use strict";

  /** The look at UTC ms `ms` — the game's LandingTheme.Decide. */
  function decide(D, ms) {
    if (!D || !D.moon) return { kind: "default" };
    for (var i = 0; i < D.moon.length; i++) {
      var m = D.moon[i];
      if (ms >= m.opens && ms < m.claimUntil) return { kind: "moon", moon: m };
    }
    var f = festival(D, ms);
    return f.open ? f : { kind: "default" };
  }

  /** FestivalSys.PhaseAt: this week's festival is open from its opening (Saturday 00:00, or Hội Trăng Rằm's own day in its week) to the
      week's end. */
  function festival(D, ms) {
    var week = Math.floor((ms - D.weekZeroMs) / D.weekMs);
    var start = week * D.weekMs + D.weekZeroMs, opens = start + D.openAfterMs, closes = start + D.closeAfterMs;
    for (var j = 0; j < D.moon.length; j++) if (D.moon[j].week === week) { opens = D.moon[j].opens; closes = D.moon[j].closes; }
    return { kind: "festival", open: ms >= opens && ms < closes, closes: closes };
  }

  var WEEKDAYS = ["Chủ nhật", "thứ Hai", "thứ Ba", "thứ Tư", "thứ Năm", "thứ Sáu", "thứ Bảy"];

  /** MoonCalendar.UseServer for the page: the window the game last heard from the server ("year,opens,closes,claimUntil", UTC ms, in
      localStorage "matu.moonwin" — written by the game, Online/Festival.cs), taken only inside its festival week (MoonCalendar.Fits),
      and the claim-day ribbon's last day said again (LandingTheme.Ribbon). `line` for a test; absent: localStorage. */
  function recall(D, line) {
    try {
      if (line === undefined) line = root.localStorage ? root.localStorage.getItem("matu.moonwin") : null;
      var w = String(line || "").split(",").map(Number);
      if (!D || !D.moon || w.length !== 4 || w.some(isNaN)) return false;
      for (var i = 0; i < D.moon.length; i++) {
        var m = D.moon[i], start = m.week * D.weekMs + D.weekZeroMs;
        if (m.year !== w[0]) continue;
        if (!(w[1] >= start + D.dayMs && w[1] < w[2] && w[2] <= start + D.closeAfterMs && w[3] >= w[2] && w[3] <= start + D.claimAfterMs))
          return false;
        m.opens = w[1]; m.closes = w[2]; m.claimUntil = w[3];
        var last = new Date(w[3] - 1 + 7 * 3600000).getUTCDay();   // the claim day's last day, Việt Nam time
        if (m.ribbon && m.ribbon.claim) m.ribbon.claim = m.ribbon.claim.replace(/(tới hết ).*$/, "$1" + WEEKDAYS[last]);
        return true;
      }
    } catch (e) {}
    return false;
  }

  /** A pinned look (?theme=) — LandingTheme.Override: the moon of the event showing, else the next one (MoonCalendar.NextYear);
      this week's festival, open or not. */
  function force(D, kind, ms) {
    if (!D || !D.moon) return { kind: "default" };
    var now = decide(D, ms);
    if (now.kind === kind) return now;
    if (kind === "moon") {
      for (var i = 0; i < D.moon.length; i++) if (D.moon[i].closes > ms) return { kind: "moon", moon: D.moon[i] };
      return { kind: "moon", moon: null };                 // past the calendar: the event's name alone
    }
    if (kind === "festival") return festival(D, ms);
    return { kind: "default" };
  }

  /** "2 ngày 5 giờ", "5 giờ 12 phút", "12 phút", "vài giây" — FestivalSys.Span. */
  function span(ms) {
    var m = Math.floor(Math.max(0, ms) / 60000);
    var d = Math.floor(m / 1440), h = Math.floor(m / 60) % 24, mi = m % 60;
    if (d > 0) return h > 0 ? d + " ngày " + h + " giờ" : d + " ngày";
    if (h > 0) return mi > 0 ? h + " giờ " + mi + " phút" : h + " giờ";
    if (mi > 0) return mi + " phút";
    return "vài giây";
  }

  /** Where Hội Trăng Rằm stands: "claim" (its Monday), "ram" (đêm rằm: 18:00 on 15/8 to the close — the moon at its biggest),
      "today" (15/8 before 18:00), "before"; null past the calendar. */
  function moonPhase(D, t, ms) {
    var m = t.moon;
    if (!m) return null;
    if (ms >= m.closes && ms < m.claimUntil) return "claim";
    if (ms >= m.evening && ms < m.closes) return "ram";
    if (ms >= m.day && ms < m.day + D.dayMs) return "today";
    return "before";
  }

  /** The event's one line — LandingTheme.Ribbon (its words for each moon window are in landing.js, written by the game). */
  function ribbon(D, t, ms) {
    if (t.kind === "moon") {
      var phase = moonPhase(D, t, ms);
      return phase ? t.moon.ribbon[phase] : D.moonName;
    }
    if (t.kind === "festival") return t.open ? D.festivalRibbon + span(t.closes - ms) : D.festivalName;
    return null;
  }

  var api = { decide: decide, force: force, span: span, ribbon: ribbon, moonPhase: moonPhase, recall: recall };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.MatuLanding = api;
})(this);
