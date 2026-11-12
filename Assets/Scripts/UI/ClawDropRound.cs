using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Audio;

namespace Cyverse.UI
{
    /// <summary>
    /// ACCESS GRABBER's round (Level 4, escalate privileges): a claw machine
    /// over a pile of keycards. One gold card belongs to a service account set
    /// up with far more access than its job needs. Steer the claw with the
    /// arrows, drop it with Space, and carry a card to the chute; the map
    /// beside the glass lights every room that card opens. Ordinary cards
    /// dead-end after a room or two, the gold one reaches the finance vault,
    /// which is the least-privilege lesson in one picture.
    /// </summary>
    public sealed class ClawDropRound : ArcadeRound
    {
        public const int DropsPerRound = 3;
        private const int WinTickets = 20;
        private const int TicketsPerSpareDrop = 10;
        private const int ConsolationTickets = 5;

        // Cabinet coordinates (centre origin, inside the cabinet box).
        private const float CabWidth = 620f, CabHeight = 430f;
        private const float RailY = 188f;
        private const float RestY = 140f;
        private const float GrabY = -84f;
        private const float CardY = -160f;
        private const float ChuteX = -262f;
        private const float MinX = -262f, MaxX = 278f;
        private const float MoveSpeed = 240f, DropSpeed = 270f, LiftSpeed = 220f, CarrySpeed = 320f;
        private const float FirmGrip = 13f, LooseGrip = 27f;
        private const float OpenAngle = 28f, ClosedAngle = 5f;

        private static readonly string[] RoomNames =
            { "LOBBY", "BREAK ROOM", "PRINTERS", "BUILD SERVERS", "BACKUPS", "FINANCE VAULT" };

        private sealed class Card
        {
            public string name;
            public string shortName;
            public int[] rooms;
            public bool gold;
            public Color color;
            public RectTransform rt;
            public float slotX;
            public float tilt;
            public bool taken;
        }

        private enum Claw { Aim, Drop, Close, Lift, Carry, Release, Reveal }

        private Card[] cards;
        private Card held;
        private bool slipping;
        private Claw claw;
        private float clawX, headY, sway, swayAmp, swayPhase, stateTime, prongAngle;
        private int dropsLeft;
        private int revealStep;
        private float lockout;

        private RectTransform cab, head, cable, prongL, prongR, goldGlow;
        private Image[] roomImages;
        private TMP_Text[] roomTexts;
        private TMP_Text mapCaption;

        protected override string MachineName => "ACCESS GRABBER";
        protected override string Tagline =>
            "Grab the <color=#FFC838><b>gold keycard</b></color>: a service account set up with far more access than it needs.\n" +
            "The map shows every room a card opens.";
        protected override string PlayKeys => "← / → MOVE     ·     SPACE DROP";
        protected override Color Accent => new Color(0.98f, 0.55f, 0.20f);

        /// <summary>Test view: drops still available.</summary>
        public int DropsLeft => dropsLeft;

        // ---- Build ------------------------------------------------------------------

        protected override void BuildPlayfield(RectTransform field)
        {
            cab = Box(field, "Cabinet", new Color(0.05f, 0.06f, 0.10f, 1f), new Vector2(-142f, 0f),
                new Vector2(CabWidth, CabHeight));
            // Back wall glow and floor give the glass some depth.
            Box(cab, "BackGlow", new Color(0.98f, 0.55f, 0.20f, 0.07f), new Vector2(0f, 40f), new Vector2(CabWidth - 20f, 260f));
            Box(cab, "Floor", new Color(0.16f, 0.12f, 0.20f, 1f), new Vector2(0f, -208f), new Vector2(CabWidth, 14f));
            Box(cab, "Rail", new Color(0.62f, 0.66f, 0.74f, 1f), new Vector2(8f, RailY), new Vector2(CabWidth - 40f, 8f));
            Box(cab, "RailShadow", new Color(0f, 0f, 0f, 0.4f), new Vector2(8f, RailY - 6f), new Vector2(CabWidth - 40f, 4f));

            RectTransform chute = Box(cab, "Chute", new Color(0.02f, 0.02f, 0.03f, 1f), new Vector2(ChuteX, -168f), new Vector2(78f, 84f));
            var chuteEdge = chute.gameObject.AddComponent<Outline>();
            chuteEdge.effectColor = new Color(1f, 0.78f, 0.22f, 0.8f);
            chuteEdge.effectDistance = new Vector2(2f, 2f);
            TMP_Text chuteLabel = Label(chute, "ChuteLabel", "PRIZE\nCHUTE", 13f, new Color(1f, 0.78f, 0.22f, 0.8f));
            chuteLabel.fontStyle = FontStyles.Bold;

            BuildCards();
            BuildClaw();
            BuildMap(field);

            clawX = 0f;
            headY = RestY;
            prongAngle = OpenAngle;
            Layout();
        }

        private void BuildCards()
        {
            var ordinary = new[]
            {
                new Card { name = "VISITOR", shortName = "VISITOR", rooms = new[] { 0 }, color = new Color(0.36f, 0.42f, 0.52f) },
                new Card { name = "INTERN", shortName = "INTERN", rooms = new[] { 0, 1 }, color = new Color(0.24f, 0.52f, 0.70f) },
                new Card { name = "PRINTER", shortName = "PRINTER", rooms = new[] { 2 }, color = new Color(0.46f, 0.36f, 0.62f) },
                new Card { name = "KIOSK", shortName = "KIOSK", rooms = new[] { 0 }, color = new Color(0.22f, 0.56f, 0.50f) },
                new Card { name = "TEMP STAFF", shortName = "TEMP", rooms = new[] { 0, 1 }, color = new Color(0.58f, 0.34f, 0.40f) },
                new Card { name = "DEV TEST", shortName = "DEV\nTEST", rooms = new[] { 3 }, color = new Color(0.30f, 0.46f, 0.34f) },
            };
            for (int i = ordinary.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (ordinary[i], ordinary[j]) = (ordinary[j], ordinary[i]);
            }

            var gold = new Card
            {
                name = "SVC-BACKUP", shortName = "SVC\nBACKUP", gold = true,
                rooms = new[] { 0, 1, 2, 3, 4, 5 }, color = new Color(1f, 0.76f, 0.20f),
            };

            cards = new Card[ordinary.Length + 1];
            // Never at either end, so it always takes a bit of steering.
            int goldSlot = Random.Range(1, cards.Length - 1);
            for (int slot = 0, next = 0; slot < cards.Length; slot++)
                cards[slot] = slot == goldSlot ? gold : ordinary[next++];

            for (int slot = 0; slot < cards.Length; slot++)
            {
                Card card = cards[slot];
                card.slotX = -178f + slot * 76f;
                card.tilt = Random.Range(-9f, 9f);
                if (card.gold)
                {
                    goldGlow = Box(cab, "GoldGlow", new Color(1f, 0.78f, 0.22f, 0.22f),
                        new Vector2(card.slotX, CardY), new Vector2(92f, 116f));
                }
                card.rt = Box(cab, "Card_" + card.name, card.color, new Vector2(card.slotX, CardY), new Vector2(62f, 86f));
                card.rt.localRotation = Quaternion.Euler(0f, 0f, card.tilt);
                Color stripe = card.gold ? new Color(0.55f, 0.36f, 0.04f) : card.color * 0.55f;
                stripe.a = 1f;
                RectTransform band = Box(card.rt, "Stripe", stripe, new Vector2(0f, 31f), new Vector2(62f, 16f));
                band.SetAsFirstSibling();
                Box(card.rt, "Chip", new Color(0.92f, 0.84f, 0.55f, 0.95f), new Vector2(-15f, 8f), new Vector2(14f, 11f));
                TMP_Text label = Label(card.rt, "Label", card.shortName, 12f,
                    card.gold ? new Color(0.20f, 0.10f, 0.0f) : Color.white);
                label.fontStyle = FontStyles.Bold;
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 13f;
                label.rectTransform.offsetMin = new Vector2(3f, 4f);
                label.rectTransform.offsetMax = new Vector2(-3f, -26f);
                label.alignment = TextAlignmentOptions.Bottom;
            }
        }

        private void BuildClaw()
        {
            cable = Box(cab, "Cable", new Color(0.70f, 0.74f, 0.80f, 1f), Vector2.zero, new Vector2(3f, 10f));
            cable.pivot = new Vector2(0.5f, 1f);
            Box(cab, "Trolley", new Color(0.85f, 0.88f, 0.94f, 1f), new Vector2(0f, RailY), new Vector2(34f, 14f));

            head = new GameObject("ClawHead", typeof(RectTransform)).GetComponent<RectTransform>();
            head.SetParent(cab, false);
            head.anchorMin = head.anchorMax = head.pivot = new Vector2(0.5f, 0.5f);
            head.sizeDelta = new Vector2(56f, 24f);
            prongL = Prong("ProngL", -17f);
            prongR = Prong("ProngR", 17f);
            Box(head, "Hub", new Color(0.86f, 0.89f, 0.95f, 1f), Vector2.zero, new Vector2(52f, 20f));
            Box(head, "HubLight", new Color(0.98f, 0.55f, 0.20f, 1f), new Vector2(0f, 1f), new Vector2(12f, 6f));
        }

        private RectTransform Prong(string name, float x)
        {
            RectTransform arm = Box(head, name, new Color(0.74f, 0.78f, 0.86f, 1f), new Vector2(x, -4f), new Vector2(7f, 44f));
            arm.pivot = new Vector2(0.5f, 1f);
            arm.anchoredPosition = new Vector2(x, -4f);
            RectTransform tip = Box(arm, "Tip", new Color(0.74f, 0.78f, 0.86f, 1f),
                new Vector2(x < 0f ? 4f : -4f, -20f), new Vector2(12f, 5f));
            tip.anchorMin = tip.anchorMax = new Vector2(0.5f, 0f);
            tip.anchoredPosition = new Vector2(x < 0f ? 4f : -4f, 2f);
            return arm;
        }

        private void BuildMap(RectTransform field)
        {
            RectTransform map = Box(field, "AccessMap", new Color(0.03f, 0.05f, 0.08f, 1f), new Vector2(318f, 0f), new Vector2(276f, CabHeight));
            TMP_Text heading = Label(map, "MapTitle", "<b>WHAT THIS CARD OPENS</b>", 16f, Dim);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(0f, 30f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            heading.characterSpacing = 3f;

            roomImages = new Image[RoomNames.Length];
            roomTexts = new TMP_Text[RoomNames.Length];
            // Rooms run from the front door at the top to the vault at the
            // bottom; links show the way in.
            for (int i = 0; i < RoomNames.Length; i++)
            {
                int col = i % 2, row = i / 2;
                var pos = new Vector2(col == 0 ? -64f : 64f, 118f - row * 92f);
                if (i % 2 == 0 && i + 1 < RoomNames.Length)
                    Box(map, "LinkH" + i, new Color(0.20f, 0.26f, 0.32f, 1f), new Vector2(0f, pos.y), new Vector2(30f, 4f));
                if (row < 2)
                    Box(map, "LinkV" + i, new Color(0.20f, 0.26f, 0.32f, 1f), new Vector2(pos.x, pos.y - 46f), new Vector2(4f, 30f));
                RectTransform room = Box(map, "Room_" + RoomNames[i], Color.white, pos, new Vector2(112f, 62f));
                roomImages[i] = room.GetComponent<Image>();
                roomTexts[i] = Label(room, "Name", RoomNames[i], 14f, Dim);
                roomTexts[i].fontStyle = FontStyles.Bold;
                if (i == RoomNames.Length - 1)
                {
                    var vaultEdge = room.gameObject.AddComponent<Outline>();
                    vaultEdge.effectColor = new Color(1f, 0.78f, 0.22f, 0.6f);
                    vaultEdge.effectDistance = new Vector2(2f, 2f);
                }
            }

            mapCaption = Label(map, "MapCaption", "Drop the claw on a card to see how far it gets.", 15f, Ink);
            mapCaption.rectTransform.anchorMin = new Vector2(0f, 0f);
            mapCaption.rectTransform.anchorMax = new Vector2(1f, 0f);
            mapCaption.rectTransform.pivot = new Vector2(0.5f, 0f);
            mapCaption.rectTransform.sizeDelta = new Vector2(-24f, 96f);
            mapCaption.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            ResetMap();
        }

        // ---- Play -------------------------------------------------------------------

        protected override void BeginPlay()
        {
            dropsLeft = DropsPerRound;
            clawX = 0f;
            headY = RestY;
            swayAmp = 4f;
            prongAngle = OpenAngle;
            claw = Claw.Aim;
            lockout = 0.25f;
            RefreshStatus();
            Flash("Line the claw up over the gold card and press <b>Space</b>. The claw swings, so time it.", 4f);
        }

        protected override void TickPlay(float dt)
        {
            stateTime += dt;
            if (lockout > 0f) lockout -= dt;
            if (goldGlow != null)
                goldGlow.GetComponent<Image>().color = new Color(1f, 0.78f, 0.22f, 0.14f + 0.10f * Mathf.Sin(Time.unscaledTime * 5f));

            switch (claw)
            {
                case Claw.Aim: TickAim(dt); break;
                case Claw.Drop:
                    headY -= DropSpeed * dt;
                    if (headY <= GrabY) { headY = GrabY; Enter(Claw.Close); }
                    break;
                case Claw.Close:
                    prongAngle = Mathf.MoveTowards(prongAngle, ClosedAngle, 90f * dt);
                    if (stateTime > 0.32f) { TryGrab(); Enter(Claw.Lift); }
                    break;
                case Claw.Lift: TickLift(dt); break;
                case Claw.Carry:
                    clawX = Mathf.MoveTowards(clawX, ChuteX, CarrySpeed * dt);
                    sway = Mathf.MoveTowards(sway, 0f, 30f * dt);
                    if (Mathf.Approximately(clawX, ChuteX)) Enter(Claw.Release);
                    break;
                case Claw.Release: TickRelease(dt); break;
                case Claw.Reveal: TickReveal(); break;
            }

            if (claw == Claw.Aim || claw == Claw.Drop)
            {
                // A loose pendulum: it swings more after you move it and
                // settles while you wait.
                swayPhase += dt * 3.1f;
                sway = swayAmp * Mathf.Sin(swayPhase);
            }
            if (held != null && !slipping && (claw == Claw.Lift || claw == Claw.Carry))
            {
                held.rt.anchoredPosition = new Vector2(HeadX + 0f, headY - 62f);
                held.rt.localRotation = Quaternion.Euler(0f, 0f, sway * 0.6f);
            }
            Layout();
        }

        private float HeadX => clawX + sway;

        private void TickAim(float dt)
        {
            float input = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) input -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) input += 1f;
            clawX = Mathf.Clamp(clawX + input * MoveSpeed * dt, MinX + 40f, MaxX);
            swayAmp = input != 0f ? Mathf.Min(15f, swayAmp + dt * 26f) : Mathf.Max(3f, swayAmp - dt * 7f);

            if (lockout <= 0f && Input.GetKeyDown(KeyCode.Space))
            {
                ResetMap();
                if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                Enter(Claw.Drop);
            }
        }

        private void TryGrab()
        {
            held = null;
            slipping = false;
            Card best = null;
            float bestDist = float.MaxValue;
            float x = HeadX;
            foreach (Card card in cards)
            {
                if (card.taken) continue;
                float d = Mathf.Abs(card.slotX - x);
                if (d < bestDist) { bestDist = d; best = card; }
            }

            dropsLeft--;
            RefreshStatus();
            if (best == null || bestDist > LooseGrip)
            {
                Flash("<color=#FF8A6A><b>Missed.</b></color> The claw came up empty.");
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                return;
            }
            held = best;
            held.rt.SetAsLastSibling();
            head.SetAsLastSibling();
            // A grab off-centre holds for a moment, then slips.
            slipping = false;
            heldLoose = bestDist > FirmGrip;
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
        }

        private bool heldLoose;
        private float slipY;

        private void TickLift(float dt)
        {
            headY = Mathf.MoveTowards(headY, RestY, LiftSpeed * dt);
            if (held != null && heldLoose && !slipping && headY > -10f)
            {
                slipping = true;
                slipY = held.rt.anchoredPosition.y;
                Flash("<color=#FF8A6A><b>It slipped!</b></color> Grab it nearer the middle of the card.");
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            }
            if (slipping && held != null)
            {
                slipY = Mathf.MoveTowards(slipY, CardY, 520f * dt);
                held.rt.anchoredPosition = new Vector2(held.slotX, slipY);
                held.rt.localRotation = Quaternion.Euler(0f, 0f, held.tilt);
                if (Mathf.Approximately(slipY, CardY)) { held = null; slipping = false; }
            }
            if (!Mathf.Approximately(headY, RestY) || slipping) return;

            prongAngle = Mathf.MoveTowards(prongAngle, OpenAngle, 90f * dt);
            if (held != null) Enter(Claw.Carry);
            else if (dropsLeft <= 0) Lose();
            else { prongAngle = OpenAngle; Enter(Claw.Aim); }
        }

        private void TickRelease(float dt)
        {
            prongAngle = Mathf.MoveTowards(prongAngle, OpenAngle, 120f * dt);
            if (held == null) return;
            Vector2 p = held.rt.anchoredPosition;
            p.y = Mathf.MoveTowards(p.y, -170f, 420f * dt);
            held.rt.anchoredPosition = p;
            if (p.y > -169f) return;

            held.taken = true;
            held.rt.gameObject.SetActive(false);
            if (held.gold && goldGlow != null) goldGlow.gameObject.SetActive(false);
            revealStep = 0;
            Enter(Claw.Reveal);
        }

        private void TickReveal()
        {
            if (held.gold)
            {
                // Light the rooms one by one, all the way down to the vault.
                if (revealStep < held.rooms.Length && stateTime > 0.22f * (revealStep + 1))
                {
                    LightRoom(held.rooms[revealStep], Gold);
                    if (Sfx.Instance != null) Sfx.Instance.PlayClick();
                    revealStep++;
                    if (revealStep == held.rooms.Length)
                        mapCaption.text = "<color=#FFC838><b>SVC-BACKUP opens every room</b></color>, right down to the finance vault.";
                }
                if (revealStep >= held.rooms.Length && stateTime > 0.22f * held.rooms.Length + 1.1f) Win();
                return;
            }

            if (revealStep == 0)
            {
                foreach (int room in held.rooms) LightRoom(room, new Color(0.30f, 0.70f, 0.95f));
                mapCaption.text = $"<b>{held.name}</b> opens {RoomList(held.rooms)}. <color=#FF8A6A>Dead end.</color>";
                revealStep = 1;
            }
            if (stateTime < 1.4f) return;
            held = null;
            if (dropsLeft <= 0) Lose();
            else { clawX = Mathf.Max(clawX, MinX + 40f); Enter(Claw.Aim); }
        }

        private void Win()
        {
            if (Phase != State.Playing) return;
            int tickets = WinTickets + TicketsPerSpareDrop * Mathf.Max(0, dropsLeft);
            Finish(true, tickets, "ACCESS GRABBED",
                "The SVC-BACKUP account could open every room, right down to the finance vault.\n" +
                "<color=#FFC838>With least privilege it would open the backups and nothing else.</color>");
        }

        private void Lose()
        {
            if (Phase != State.Playing) return;
            Finish(false, ConsolationTickets, "OUT OF DROPS",
                "The gold SVC-BACKUP card stayed in the pile. It was the one account\n" +
                "with far more access than its job needs, so it was the way in.");
        }

        protected override void ResolveForTest(bool win)
        {
            if (win)
            {
                foreach (int room in new[] { 0, 1, 2, 3, 4, 5 }) LightRoom(room, Gold);
                Win();
            }
            else
            {
                dropsLeft = 0;
                Lose();
            }
        }

        /// <summary>Test hook: park the claw over the gold card, still.</summary>
        public void AimAtGoldForTest()
        {
            foreach (Card card in cards)
                if (card.gold && !card.taken) clawX = card.slotX;
            swayAmp = 0f;
            sway = 0f;
            Layout();
        }

        /// <summary>Test hook: press drop without a key.</summary>
        public void DropForTest()
        {
            if (Phase != State.Playing || claw != Claw.Aim) return;
            ResetMap();
            Enter(Claw.Drop);
        }

        /// <summary>Test view: a card is in the claw.</summary>
        public bool HoldingCard => held != null && (claw == Claw.Lift || claw == Claw.Carry);

        // ---- Drawing ----------------------------------------------------------------

        private void Enter(Claw next)
        {
            claw = next;
            stateTime = 0f;
        }

        private void Layout()
        {
            if (head == null) return;
            head.anchoredPosition = new Vector2(HeadX, headY);
            head.localRotation = Quaternion.Euler(0f, 0f, -sway * 0.5f);
            prongL.localRotation = Quaternion.Euler(0f, 0f, -prongAngle);
            prongR.localRotation = Quaternion.Euler(0f, 0f, prongAngle);
            float top = RailY - 4f;
            cable.anchoredPosition = new Vector2(clawX + sway * 0.5f, top);
            cable.sizeDelta = new Vector2(3f, Mathf.Max(4f, top - headY - 8f));
            cable.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(sway * 0.5f, top - headY) * Mathf.Rad2Deg);
            Transform trolley = cab.Find("Trolley");
            if (trolley != null) ((RectTransform)trolley).anchoredPosition = new Vector2(clawX, RailY);
        }

        private void RefreshStatus()
        {
            var dots = new System.Text.StringBuilder();
            for (int i = 0; i < DropsPerRound; i++)
                dots.Append(i < dropsLeft ? "<color=#FFC838>●</color> " : "<color=#3A4654>●</color> ");
            SetStatus($"DROPS  {dots}    ·    GRAB THE CARD WITH TOO MUCH ACCESS");
        }

        private void ResetMap()
        {
            if (roomImages == null) return;
            for (int i = 0; i < roomImages.Length; i++)
            {
                roomImages[i].color = new Color(0.07f, 0.09f, 0.13f, 1f);
                roomTexts[i].color = Dim;
            }
            if (mapCaption != null)
                mapCaption.text = "Drop the claw on a card to see how far it gets.";
        }

        private void LightRoom(int index, Color color)
        {
            if (roomImages == null || index < 0 || index >= roomImages.Length) return;
            roomImages[index].color = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);
            roomTexts[index].color = Color.white;
        }

        private static string RoomList(int[] rooms)
        {
            if (rooms.Length == 1) return RoomNames[rooms[0]];
            var parts = new string[rooms.Length];
            for (int i = 0; i < rooms.Length; i++) parts[i] = RoomNames[rooms[i]];
            return string.Join(", ", parts, 0, parts.Length - 1) + " and " + parts[parts.Length - 1];
        }
    }
}
