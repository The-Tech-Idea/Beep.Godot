using Godot;
using System.Collections.Generic;

namespace Beep.ECS.UI.Kit
{
    /// <summary>
    /// Nine-slice artwork for the kit's plates, tinted by the palette.
    ///
    /// WHY THIS EXISTS ALONGSIDE THE PROCEDURAL STACK
    /// ----------------------------------------------
    /// The kit draws a plate as flat fill plus a thin rim plus a low-alpha bevel. On a dark skin
    /// that has almost nowhere to go: rendered and measured, a whole pass of tuning moved 0.36% of
    /// the pixels in the button band. Real game controls get their depth from artwork, not from
    /// arithmetic over the surface colour.
    ///
    /// The addon HAD texture chrome once and it was removed, thoroughly — the contract scan still
    /// bans nineteen `textures/&lt;genre&gt;/&lt;theme&gt;/` folders. That removal was right about the design
    /// it removed: one art set per genre AND per theme cannot scale to 51 themes, and the sets
    /// drift. This is the other design. ONE neutral set, tinted per palette.
    ///
    /// It works because the source art is neutral. Measured on the imported files, the Kenney Grey
    /// variant sits at 0.056 centre saturation — pure shading with no colour of its own to fight
    /// the palette. The coloured variants measure 0.757 and are useless for this; they are
    /// deliberately not imported.
    ///
    /// The art is RE-TINTED per palette colour and cached, not modulated. A multiply was tried
    /// first and measured: it carries the artwork faithfully onto a light plate and vanishes on a
    /// dark one, because multiplying a 0.10 face by the sprite's own 0.61-to-1.00 ramp compresses
    /// the whole sculpt into four hundredths of luminance. See <see cref="LiftShare"/>.
    ///
    /// WHAT IT DOES NOT DO
    /// -------------------
    /// It does not replace the silhouette system. <see cref="KitShape"/> has 21 outlines and a
    /// genre picks one; a nine-slice sprite has exactly one, a rounded rectangle. A genre whose
    /// identity is a torn edge, a chamfer or a speed wedge keeps drawing procedurally. The sprite
    /// material is opt-in per genre through <see cref="KitGeometry.Material"/>, which a theme's
    /// `kit.material` key can override, so the two coexist the way <see cref="KitRegister"/>
    /// variants already do.
    ///
    /// It also does not cover every widget class. <see cref="KitWidgetClass.Bar"/> has no entry on
    /// purpose: the only neutral bar art in the pack (`slide_horizontal_grey`) is an 8px track
    /// centred in a 16px canvas with transparent padding above and below, so stretching it across a
    /// meter would leave the meter's plate half transparent and misaligned with the fill drawn over
    /// it. A meter keeps the procedural plate until there is art that is actually a plate.
    /// </summary>
    public static class KitSprite
    {
        /// <summary>The material name meaning "no artwork, draw procedurally". Also what an empty
        /// or unknown name resolves to.</summary>
        public const string Procedural = "";

        private const string Dir = "res://addons/beep_game_builder_cs/textures/kit/";

        /// <summary>The nine-slice insets of a sprite, in source pixels.</summary>
        public readonly struct Slice
        {
            public readonly float Left, Right, Top, Bottom;
            public Slice(float left, float right, float top, float bottom)
            {
                Left = left; Right = right; Top = top; Bottom = bottom;
            }
        }

        /// <summary>Everything a caller needs to paint one plate: which art, where it goes, how it
        /// slices, and the modulate — which carries the caller's ALPHA only, since the palette
        /// colour is already baked into the texture.</summary>
        public readonly struct Plate
        {
            public readonly Texture2D Texture;
            public readonly Rect2 Rect;
            public readonly Slice Margins;
            public readonly Color Modulate;

            public Plate(Texture2D texture, Rect2 rect, Slice margins, Color modulate)
            {
                Texture = texture; Rect = rect; Margins = margins; Modulate = modulate;
            }
        }

        private sealed class Entry
        {
            public required string File;
            public required Slice Margins;

            /// <summary>
            /// The luminance of the sprite's FACE band — the flat area the palette colour should
            /// land on exactly, with everything above it reading as highlight and everything below
            /// as shadow.
            ///
            /// Measured per file, not assumed: the flat and metal art carry a 0.864 face, the
            /// gradient art a 0.941 one. It is the only number in the table a machine cannot
            /// derive, because "which band is the face" is a judgement about what the artist drew,
            /// not a property of the histogram. The two ENDS of the ramp are measured off the
            /// image at load time instead — see <see cref="Art"/>.
            /// </summary>
            public required float FaceLuma;

            /// <summary>
            /// The art for the same plate with its raised bottom lip removed, drawn shifted down by
            /// <see cref="LipPx"/> so a pressed control actually SINKS.
            ///
            /// This is the reason a depth sprite is worth having at all. Fading or darkening a
            /// button on press is the themed-form tell the kit's own state rule already calls out;
            /// the artwork lets the press be a movement instead. Null where the plate has no lip to
            /// lose — a flat slot or chip has nothing to sink into.
            /// </summary>
            public string? PressedFile;

            /// <summary>Height of the raised lip in SOURCE pixels, scaled to the destination when
            /// the pressed art is placed. 0 where there is none.</summary>
            public float LipPx;
        }

        /// <summary>
        /// What each widget class is drawn from, per material. Insets are measured off the files,
        /// not guessed: each is large enough to contain that sprite's whole edge construction and
        /// its corner, so nine-slicing keeps them at their authored size instead of stretching
        /// them into bands.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<KitWidgetClass, Entry>> Materials = new()
        {
            ["ui_pack"] = new()
            {
                // 192x64. Edges: 2px rim at 0.61, 2px keyline at 1.00, then the face. The face is a
                // vertical gradient, 1.00 down to 0.88, and the bottom carries a RAISED LIP —
                // 2px keyline, 2px rim, then 4px of dark side face at 0.41. That lip is eight
                // pixels tall, hence the deeper bottom inset: slicing it symmetrically at 8 would
                // stretch the lip into a band that grows with the button instead of staying an
                // edge.
                //
                // `button_rectangle_gradient` is the same face with the lip removed and a plain
                // 4px bottom edge — measured identical through the gradient, so the two read as
                // one control up and down rather than as two different buttons.
                [KitWidgetClass.Button] = new Entry
                {
                    File = "button_rectangle_depth_gradient.png",
                    Margins = new Slice(8f, 8f, 8f, 10f),
                    FaceLuma = 0.941f,
                    PressedFile = "button_rectangle_gradient.png",
                    LipPx = 4f,
                },

                // 100x100. 2px rim at 0.64, 2px keyline at 0.95, flat 0.86 face — and a 4x4 RIVET
                // at inset 6..9 in each corner. The inset has to clear the rivet or the corner
                // patch cuts it in half and the stretched edge smears the other half along the
                // panel's side.
                [KitWidgetClass.Panel] = new Entry
                {
                    File = "panel_metal.png",
                    Margins = new Slice(12f, 12f, 12f, 12f),
                    FaceLuma = 0.864f,
                },

                // 64x64, the same 2px rim + 2px keyline edge with a flat face. No lip, so no
                // pressed variant: a slot has nothing to sink.
                [KitWidgetClass.Slot] = new Entry
                {
                    File = "button_square_flat.png",
                    Margins = new Slice(8f, 8f, 8f, 8f),
                    FaceLuma = 0.864f,
                },

                // No Chip entry, and none is coming from this pack. Every genre's chip silhouette
                // is a Pill (or a Parallelogram for the sci-fi pair), and a nine-slice is a
                // rounded rectangle — rendered, the rarity and level chips stopped being pills and
                // became small rectangles, which is precisely the silhouette loss this material is
                // supposed to avoid. See FitsSilhouette: the rule is enforced there, and this entry
                // would be unreachable under it even if it existed.
            },
        };

        /// <summary>
        /// Whether a nine-slice can honestly stand in for this silhouette.
        ///
        /// A sprite has exactly ONE outline, a rounded rectangle, so it may replace only the
        /// silhouettes that already are one. Everything else in <see cref="KitShape"/> — the pill,
        /// the chamfer, the speed wedge, the shield, the torn parchment, the sci-fi asymmetric cut
        /// — is a genre's or a widget's tell, and swapping it for artwork would buy depth by
        /// spending identity. <see cref="KitShape.Stepped"/> is excluded for the opposite reason:
        /// the pixel register's whole point is a quantised staircase, and an anti-aliased corner
        /// is the giveaway it exists to eliminate.
        ///
        /// This lives here rather than at the call sites because it is a fact about the ART, not
        /// about any one widget.
        /// </summary>
        public static bool FitsSilhouette(KitShape shape)
            => shape is KitShape.Rect or KitShape.Round;

        /// <summary>One sprite's decoded pixels plus the two ends of its own luminance ramp,
        /// measured on load so the table does not carry numbers that can go stale when the art is
        /// replaced.</summary>
        private sealed class Art
        {
            public required byte[] Pixels;   // RGBA8, row-major
            public required int Width;
            public required int Height;
            /// <summary>Darkest and brightest OPAQUE luminance in the file.</summary>
            public required float Min;
            public required float Max;
        }

        private static readonly Dictionary<string, Art?> _art = new();
        private static readonly HashSet<string> _warned = new();

        /// <summary>
        /// Where a control keeps the tinted plates it has drawn with.
        ///
        /// The cache is per CONTROL rather than static, and that is an ownership decision, not a
        /// performance one. A static C# field holding a Godot RefCounted keeps it alive past the
        /// point the engine tears the mono runtime down, and its instance-binding callback then
        /// fires with no owner left: measured, that is a hard process crash
        /// (Condition "!rc_owner" is true, exit 0xC000001D) AFTER every assertion has already
        /// passed -- theme_gallery_layout_probe printed OK and then died. Disposing the
        /// intermediates was not enough on its own; the cached textures themselves had to be owned
        /// by something the engine frees first.
        ///
        /// Node metadata is exactly that owner. Godot holds the reference and releases it when the
        /// node is freed, during normal scene teardown rather than at process exit. The cost is
        /// that two widgets on one skin each tint their own copy instead of sharing one, which is
        /// a few hundred microseconds of byte arithmetic on a theme change and not worth trading a
        /// crash for.
        /// </summary>
        private const string CacheMeta = "_beep_kit_sprite_plates";

        /// <summary>
        /// Why the artwork is RE-TINTED rather than multiplied, and how much of a clipped side's
        /// unspent reach is handed to the opposite side.
        ///
        /// A multiply preserves the art's RATIOS, which is fine on a mid or light plate and
        /// useless on a dark one: on citybuilder's near-black icon buttons (face luminance 0.10)
        /// the sprite's 0.61 rim lands at 0.061 and its 1.00 keyline at 0.10, a separation of four
        /// hundredths that no eye resolves. Rendered and looked at, the dark widgets were
        /// unchanged while the amber ones gained a gradient — the same asymmetry the panel wells
        /// had before <c>KitChrome.RecessFace</c>.
        ///
        /// So the ramp is ANCHORED: the sprite's face band lands exactly on the palette colour and
        /// the rest of the artwork keeps its own distances from it. When the palette is too dark to
        /// carry the shadow side, that side compresses and its unspent reach goes to the highlight
        /// — whole, which is what turns a dark button back into a button. Above 1 this would start
        /// inventing contrast the artist never drew.
        ///
        /// An earlier version squeezed BOTH sides into a fixed ±0.20 window instead. That is a
        /// three-fold compression of this art (its lip sits 0.53 below its face) and it rendered
        /// the sprite as a barely-tilted flat plate — the artwork arrived and then had its whole
        /// point removed on the way in. Measured: switching material moved 3.15% of the page.
        /// </summary>
        private const float LiftShare = 1f;

        /// <summary>Whether a material name names a sprite set this build ships. An unknown name is
        /// reported once — silently falling back to procedural would make a misspelled
        /// `kit.material` indistinguishable from a genre that never opted in.</summary>
        public static bool HasMaterial(string? material)
        {
            if (string.IsNullOrEmpty(material)) return false;
            if (Materials.ContainsKey(material!)) return true;
            if (_warned.Add("material/" + material))
                GD.PushWarning($"[KitSprite] '{material}' is not a sprite set this build ships, so "
                             + "widgets asking for it draw procedurally. Known sets: "
                             + string.Join(", ", Materials.Keys));
            return false;
        }

        /// <summary>
        /// Resolve the plate for a widget, or false when this genre draws procedurally or the set
        /// has no art for that class — in which case the caller draws exactly as it did before.
        /// </summary>
        public static bool TryPlate(CanvasItem owner, KitGeometry g, KitWidgetClass widgetClass,
                                    KitState state, KitShape shape, Rect2 body, Color face,
                                    out Plate plate)
        {
            plate = default;

            if (!FitsSilhouette(shape)
                || string.IsNullOrEmpty(g.Material)
                || !Materials.TryGetValue(g.Material, out var byClass)
                || !byClass.TryGetValue(widgetClass, out Entry? entry))
                return false;

            // A pressed plate drops by its own lip height and gives that height back to the
            // ground, so the control's bottom edge stays put and its top edge travels. Scaled from
            // source to destination, because the same art serves a 28px chip and a 96px tile.
            bool sunken = state == KitState.Pressed && entry.PressedFile != null && entry.LipPx > 0f;
            string file = sunken ? entry.PressedFile! : entry.File;

            if (Tinted(owner, file, entry.FaceLuma, face) is not { } texture) return false;

            Rect2 rect = body;
            if (sunken)
            {
                float srcHeight = Mathf.Max(1f, texture.GetSize().Y);
                float drop = Mathf.Min(entry.LipPx * (body.Size.Y / srcHeight), body.Size.Y * 0.25f);
                rect = new Rect2(body.Position.X, body.Position.Y + drop,
                                 body.Size.X, body.Size.Y - drop);
            }

            // The colour is already baked into the texture, so the modulate carries only the
            // caller's alpha. Multiplying by the face a second time here would darken it twice.
            plate = new Plate(texture, rect, entry.Margins, new Color(1f, 1f, 1f, face.A));
            return true;
        }

        /// <summary>The weighted grey of a colour. Deliberately the same coefficients the art was
        /// measured with, applied to the same gamma-encoded values — this is a tone weight for
        /// placing a pixel on the artwork's ramp, not a contrast metric.</summary>
        private static float Tone(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;

        /// <summary>
        /// The sprite re-tinted so its face band is <paramref name="face"/>, cached.
        ///
        /// Keyed on the colour quantised to 6 bits a channel: a skin uses a handful of distinct
        /// face colours across its roles and states, so the cache holds a handful of images, and
        /// quantising stops a one-bit rounding difference from minting a duplicate. The whole
        /// cache is dropped when it grows past its cap rather than evicted one entry at a time —
        /// the images are small and a theme switch invalidates most of them together anyway.
        /// </summary>
        private static ImageTexture? Tinted(GodotObject owner, string file, float faceLuma, Color face)
        {
            int qr = (int)(Mathf.Clamp(face.R, 0f, 1f) * 63f + 0.5f);
            int qg = (int)(Mathf.Clamp(face.G, 0f, 1f) * 63f + 0.5f);
            int qb = (int)(Mathf.Clamp(face.B, 0f, 1f) * 63f + 0.5f);
            string key = $"{file}#{qr},{qg},{qb}";

            Godot.Collections.Dictionary cache;
            if (owner.HasMeta(CacheMeta))
            {
                cache = owner.GetMeta(CacheMeta).AsGodotDictionary();
                if (cache.TryGetValue(key, out Variant hit) && hit.As<ImageTexture>() is { } cached)
                    return cached;
            }
            else
            {
                cache = new Godot.Collections.Dictionary();
                owner.SetMeta(CacheMeta, cache);
            }

            if (Load(file) is not { } art) return null;

            // KEEP THE ARTWORK'S OWN CONTRAST. Gain 1.0 means the sprite's lip, rim, face and
            // keyline land the same distance apart on the palette colour as the artist drew them;
            // the palette moves the whole ramp, it does not flatten it. Anything less is a
            // deliberate softening, and softening the art is how a nine-slice ends up looking like
            // the flat plate it replaced.
            //
            // The gain drops below 1.0 only when the palette has no room: a face at 0.10 luminance
            // cannot carry a shadow that reaches 0.53 below it, so that side compresses to fit.
            // What it cannot spend downward is handed to the highlight instead, which is what
            // keeps a dark skin's controls sculpted rather than flat -- the same principle as
            // KitChrome.RecessFace.
            float outFace = Mathf.Clamp(Tone(face.R, face.G, face.B), 0f, 1f);
            float belowSpan = Mathf.Max(0.001f, faceLuma - art.Min);
            float aboveSpan = Mathf.Max(0.001f, art.Max - faceLuma);

            float gainDown = Mathf.Min(1f, outFace / belowSpan);
            float gainUp = Mathf.Min(1f, (1f - outFace) / aboveSpan);

            // Hand the clipped side's unspent reach to the other one, up to what that side can
            // hold. On a near-black plate this is the whole visible sculpt.
            float unspentDown = (1f - gainDown) * belowSpan;
            if (unspentDown > 0.001f)
                gainUp = Mathf.Min((1f - outFace) / aboveSpan,
                                   gainUp + unspentDown * LiftShare / aboveSpan);
            float unspentUp = (1f - gainUp) * aboveSpan;
            if (unspentUp > 0.001f)
                gainDown = Mathf.Min(outFace / belowSpan,
                                     gainDown + unspentUp * LiftShare / belowSpan);

            byte[] src = art.Pixels;
            var dst = new byte[src.Length];
            for (int i = 0; i < src.Length; i += 4)
            {
                byte alpha = src[i + 3];
                if (alpha == 0) continue;   // dst is already zeroed

                float tone = Tone(src[i] / 255f, src[i + 1] / 255f, src[i + 2] / 255f);
                float target = outFace + (tone - faceLuma) * (tone >= faceLuma ? gainUp : gainDown);
                target = Mathf.Clamp(target, 0f, 1f);

                float r, g, b;
                if (target <= outFace)
                {
                    // Scaling toward black keeps the palette's hue exactly.
                    float k = outFace > 0.0001f ? target / outFace : 0f;
                    r = face.R * k; g = face.G * k; b = face.B * k;
                }
                else
                {
                    // Lerping toward white lands on `target` by construction, because white
                    // contributes its full tone and the face contributes the rest.
                    float u = (target - outFace) / Mathf.Max(0.0001f, 1f - outFace);
                    r = face.R + (1f - face.R) * u;
                    g = face.G + (1f - face.G) * u;
                    b = face.B + (1f - face.B) * u;
                }

                dst[i] = (byte)Mathf.Clamp(r * 255f + 0.5f, 0f, 255f);
                dst[i + 1] = (byte)Mathf.Clamp(g * 255f + 0.5f, 0f, 255f);
                dst[i + 2] = (byte)Mathf.Clamp(b * 255f + 0.5f, 0f, 255f);
                dst[i + 3] = alpha;
            }

            if (cache.Count >= TintCacheLimit) cache.Clear();

            // The intermediate Image is disposed rather than left to a finalizer, for the same
            // reason the cache lives on the node: a Godot RefCounted released during engine
            // teardown fires its instance-binding callback with no owner left.
            using var image = Image.CreateFromData(art.Width, art.Height, false,
                                                   Image.Format.Rgba8, dst);
            var texture = ImageTexture.CreateFromImage(image);
            cache[key] = texture;
            return texture;
        }

        /// <summary>How many tinted variants one control holds before it starts over. A widget
        /// needs its normal, hover and pressed faces and little else; the cap only stops a control
        /// animated through a colour ramp from growing without bound.</summary>
        private const int TintCacheLimit = 8;

        private static Art? Load(string file)
        {
            if (_art.TryGetValue(file, out Art? cached)) return cached;

            string path = Dir + file;
            Texture2D? texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Image? image = texture?.GetImage();
            if (image == null)
            {
                GD.PushWarning($"[KitSprite] '{path}' could not be read, so widgets asking for this "
                             + "material fall back to procedural drawing.");
                _art[file] = null;
                return null;
            }

            if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
            byte[] pixels = image.GetData();
            int width = image.GetWidth(), height = image.GetHeight();
            image.Dispose();   // see Tinted: no Godot object outlives this method by accident

            float min = 1f, max = 0f;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] < 8) continue;   // anti-aliased fringe is not part of the ramp
                float tone = Tone(pixels[i] / 255f, pixels[i + 1] / 255f, pixels[i + 2] / 255f);
                if (tone < min) min = tone;
                if (tone > max) max = tone;
            }

            var art = new Art
            {
                Pixels = pixels,
                Width = width,
                Height = height,
                Min = min,
                Max = max,
            };
            _art[file] = art;
            return art;
        }

        /// <summary>Paint a resolved plate.</summary>
        public static void Draw(CanvasItem ci, Plate plate)
            => DrawNineSlice(ci, plate.Texture, plate.Rect, plate.Margins, plate.Modulate);

        /// <summary>
        /// Draw a nine-sliced sprite into <paramref name="dst"/>, multiplied by
        /// <paramref name="modulate"/>.
        ///
        /// Godot's nine-slice lives on NinePatchRect and StyleBoxTexture, and neither is reachable
        /// from a custom `_Draw`, so the nine regions are placed here. Corners keep their source
        /// size; edges stretch along one axis; the centre stretches on both. When the destination
        /// is smaller than the corners alone, the insets are scaled down together rather than
        /// allowed to overlap, which is what turns a small button into a smear.
        /// </summary>
        public static void DrawNineSlice(CanvasItem ci, Texture2D texture, Rect2 dst,
                                         Slice margins, Color modulate)
        {
            Vector2 src = texture.GetSize();
            if (src.X <= 0f || src.Y <= 0f || dst.Size.X <= 0f || dst.Size.Y <= 0f) return;

            float left = margins.Left, right = margins.Right;
            float top = margins.Top, bottom = margins.Bottom;

            // Shrink the insets together if the target cannot hold them, so opposite corners never
            // cross over each other.
            float wantedX = left + right;
            if (wantedX > dst.Size.X && wantedX > 0f)
            {
                float k = dst.Size.X / wantedX;
                left *= k; right *= k;
            }
            float wantedY = top + bottom;
            if (wantedY > dst.Size.Y && wantedY > 0f)
            {
                float k = dst.Size.Y / wantedY;
                top *= k; bottom *= k;
            }

            // Source and destination column/row edges, in the same order. The SOURCE edges keep the
            // authored insets even when the destination shrank its own: the corner art is scaled
            // down, not cropped.
            float[] sx = { 0f, margins.Left, src.X - margins.Right, src.X };
            float[] sy = { 0f, margins.Top, src.Y - margins.Bottom, src.Y };
            float[] dx = { dst.Position.X, dst.Position.X + left, dst.End.X - right, dst.End.X };
            float[] dy = { dst.Position.Y, dst.Position.Y + top, dst.End.Y - bottom, dst.End.Y };

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var source = new Rect2(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
                    var target = new Rect2(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]);
                    if (source.Size.X <= 0f || source.Size.Y <= 0f) continue;
                    if (target.Size.X <= 0f || target.Size.Y <= 0f) continue;
                    ci.DrawTextureRectRegion(texture, target, source, modulate);
                }
            }
        }
    }
}
