using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Minigames;

namespace DesertBus;

public class GameData
{
    public float Scale;
    public Vector2 Size;
    public List<GameRules> Rules;
}

public class GameRules
{
    // location
    public string From = null;
    public string To = null;
    // bus model
    public int Speed = 35;
    public int MaxSpeed = 70;
    public double Acceleration = 1.5d;
    public double Deceleration = -0.666d;
    public double Braking = -10.0d;
    public double SteeringLimit = 1.0d;
    public double SteeringToWheel = 0.5d;
    public double SteeringToGround = 0.0075d;
    public double SteeringDecay = -0.5d;
    public double SteeringRotations = 3d;
    public double PositionDrift = 5d;
    // others
    public double BugSplatDistance = 362000;
    // road conditions
    public int Width = 100;
    public int Distance = 580000;
    // lose conditions
    public int FailTime = 5000;
    public double FailTimeDecay = -0.25d;
    // rule conditions
    public string Condition = null;
}

public class GameState
{
    public long PlayerID;
    public string PlayerName;
    public string From;
    public string To;
    public double Position;
    public double Distance;
}

public class Game : IMinigame
{
    public static ICue StartNoise;
    public static ICue EngineNoise;
    public static ICue RoadNoise;

    public static Texture2D Sprites;
    public BasicEffect basicEffect;

    public GameData Data;
    public GameRules Rules;
    public GameState State;

    public Clock Clock;
    public Odometer Odometer;

    public Pool<Decor> Decor;

    public Rectangle View;
    public float Opacity;
    public Vector2 Shake;

    public int DoorsOpen;
    public float DoorsTimer;

    public float BugSplat;

    public double Speed;
    public double WheelSpeed;
    public double WheelRotation;

    public bool EngineOn;
    public double EngineTimer;

    public double FailTimer;
    public double EndGameTimer;

    public int Fade;
    public bool Quit;

    public bool Success => this.Rules.Distance > 0 && this.State.Distance >= this.Rules.Distance;
    public bool Failure => this.Speed <= 0 && this.FailTimer >= this.Rules.FailTime;

    public Game(GameData data, GameRules rules, GameState state)
    {
        Texture2D sprites = ModEntry.Instance.Helper.ModContent.Load<Texture2D>("assets/sprites.png");

        Game.Sprites = sprites;
        this.basicEffect = new(Game1.graphics.GraphicsDevice);

        this.Data = data;
        this.Rules = rules;
        this.State = state;

        this.Clock = new();
        this.Odometer = new(digits: 6u, start: 601093d);

        this.Decor = new(size: 8, create: () => new Decor());
        this.Opacity = 0;
        this.Shake = Vector2.Zero;

        this.DoorsOpen = 1;
        this.DoorsTimer = 1;

        this.BugSplat = this.State.Distance < this.Rules.BugSplatDistance ? 0 : 5;

        this.Speed = this.Rules.Speed;
        this.WheelSpeed = 0;
        this.WheelRotation = 0;

        this.EngineOn = this.Speed > 0;
        this.EngineTimer = 0;

        this.FailTimer = 0;
        this.EndGameTimer = 5000;

        this.Fade = 0;
        this.Quit = false;

        // add roadside decor
        for (int i = 0; i < Game1.random.Next(3); ++i)
            this.AddDecor(randomY: true);

        // add bus stop
        if (this.Speed <= 0)
        {
            Decor decor = this.Decor.Get();
            decor.Position = 0.01d;
            decor.Distance = 0.3d;
            decor.Sprites = new Dictionary<float, Rectangle>{
                {0f, new(323, 477, 9, 19)},
            };
        }

        Game1.playSound("roadnoise", out Game.RoadNoise);
        if (Game.RoadNoise is not null)
        {
            Game.RoadNoise.SetVariable("Volume", 0);
            Game.RoadNoise.Resume();
        }
        Game1.playSound("heavyEngine", out Game.EngineNoise);
        if (Game.EngineNoise is not null)
        {
            Game.EngineNoise.SetVariable("Volume", 0);
            Game.EngineNoise.Resume();
        }

        this.changeScreenSize();
    }

    public void changeScreenSize()
    {
        Rectangle viewport = Game1.game1.localMultiplayerWindow;
        Point size = (this.Data.Size * this.Data.Scale).ToPoint();
        this.View = new(viewport.X + viewport.Width / 2 - size.X / 2, viewport.Y + viewport.Height / 2 - size.Y / 2, size.X, size.Y);
    }

    public void AddDecor(bool randomY = false)
    {
        Decor decor = this.Decor.Get();
        decor.Position = Game1.random.NextDouble() - 0.5d;
        decor.Distance = randomY ? Game1.random.NextDouble() * 0.25d : 0d;
        if (0d < decor.Position && decor.Position < 0.05d && this.State.Distance % 10 == 0)
            // sign
            decor.Sprites = new Dictionary<float, Rectangle>{
                {0.3f, new(323, 477, 9, 19)},
                {0.1f, new(362, 395, 7, 13)},
                {0f, new(375, 404, 4, 4)},
            };
        else
            // bush
            decor.Sprites = new Dictionary<float, Rectangle>{
                {0.3f, new(71, 1931, 21, 21)},
                {0.1f, new(137, 412, 10, 11)},
                {0f, new(433, 451, 3, 3)},
            };
    }

    public void Engine()
    {
        if (!this.EngineOn)
        {
            this.EngineOn = true;
            this.EngineTimer = 2000;
            if (Game.StartNoise is null || !Game.StartNoise.IsPlaying)
            {
                Game1.playSound("busDriveOff", out Game.StartNoise);
            }
        }
    }

    public void Honk()
    {
        Game1.playSound("Duck", pitch: 0);
    }

    public void Doors()
    {
        Game1.playSound("trashcanlid", pitch: this.DoorsOpen > 0 ? 100 : 0);
        this.DoorsOpen *= -1;
    }

    public void draw(SpriteBatch b)
    {
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState());
        
        Rectangle view = Game1.game1.localMultiplayerWindow;
        float scale = this.Data.Scale;
        Vector2 position;
        Rectangle source;
        Vector2 shake = this.Shake + new Vector2(0, 3f * (float)Math.Sin(this.State.Distance / 20d)) * scale;
        Color colour = Color.White;
        float alpha = 1f;

        float horizon = 0.333f;
        float median = (float)-this.State.Position / 100;

        // transform coordinates in (-1~1) space to increase x value range as y approaches 1
        double perspective(Vector2 position, float ratio)
        {
            // vanishing point
            Vector2 far = new(-0.125f + position.X / 2, horizon);

            // near point
            Vector2 near = new(-0.1f + position.X / position.X * Math.Sign(position.X) * 1.75f + median, position.Y - horizon);

            return Vector2.Lerp(far, near, ratio).X;
        }

        // BACKGROUND

        // sky
        Utility.DrawSquare(b, new(this.View.Left, this.View.Top, this.View.Width, this.View.Height / 3), 0, null, Color.LightSkyBlue);
        Utility.DrawSquare(b, new(this.View.Left, this.View.Top + this.View.Height / 13 * 4, this.View.Width, this.View.Height / 3 * 2), 0, null, Color.LightBlue);
        // sand
        Utility.DrawSquare(b, new(this.View.Left, this.View.Top + this.View.Height / 3, this.View.Width, this.View.Height / 3 * 2), 0, null, Color.Orange);
        // DEBUG: road
        if (ModEntry.Debug)
        {
            int median1 = -(int)this.State.Position;
            Utility.drawLineWithScreenCoordinates(this.View.Center.X + median1, this.View.Top, this.View.Center.X + median1, this.View.Bottom, b, Color.Black, 1, 1);
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState());
        
        // road
        {
            //Vector2 size = this.View.Size.ToVector2();
            Vector2 viewRatio = this.View.Size.ToVector2() / view.Size.ToVector2();
            float z = 1f;
            Vector3 viewToVertex(Vector2 position)
            {
                return new Vector3((new Vector2(position.X, position.Y) * 2f - Vector2.One) * viewRatio, z -= 0.0001f);
            }

            this.basicEffect = new(Game1.graphics.GraphicsDevice);
            this.basicEffect.VertexColorEnabled = true;

            // fill colour
            float width = this.Rules.Width / 60f;

            Vector2 top = new(-0.25f + (width * 0.375f), 1f - horizon);
            Vector2 left = new(-0.25f + 0 + median, 0);
            Vector2 centre = new(-0.25f + width / 2 + median, 0);
            Vector2 right = new(-0.25f + width + median, 0);

            Vector3[] vertices = [
                viewToVertex(left),
                viewToVertex(top),
                viewToVertex(right),
            ];
            var triangles = vertices.Select(v => new VertexPositionColor(v, new(95, 90, 95))).ToArray();

            const int num = 9;
            for (int i = 0; i < num; ++i)
            {
                double value = this.State.Distance / 3;
                double rate = 100;
                double d = (double)i / num;
                double t = (value + d * rate) % rate / rate;
                float ratio = (float)Math.Pow(t, 2);
                Vector2 size = new(0.025f, 0.0125f);

                float ratioT = Math.Max(0, ratio - size.Y * 4 * ratio);
                float ratioB = Math.Max(0, ratio + size.Y * 4 * ratio);

                Vector2 TL = new(-size.X, +size.Y);
                Vector2 TR = new(+size.X, +size.Y);
                Vector2 BL = new(-size.X, -size.Y);
                Vector2 BR = new(+size.X, -size.Y);

                Vector2 lerpTL = Vector2.Lerp(top, centre + TL, (float)Math.Pow(ratioT, 2));
                Vector2 lerpTR = Vector2.Lerp(top, centre + TR, (float)Math.Pow(ratioT, 2));
                Vector2 lerpBR = Vector2.Lerp(top, centre + BR, (float)Math.Pow(ratioB, 2));
                Vector2 lerpBL = Vector2.Lerp(top, centre + BL, (float)Math.Pow(ratioB, 2));

                vertices = [
                    // desperate times
                    viewToVertex(lerpBL), // BL
                    viewToVertex(lerpTR), // TR
                    viewToVertex(lerpBR), // BR
                    //
                    viewToVertex(lerpBL), // BL
                    viewToVertex(lerpTL), // TL
                    viewToVertex(lerpTR), // TR
                ];
                triangles = triangles.Concat(vertices.Select(v => new VertexPositionColor(v, new Color(245, 185, 0)))).ToArray();
            }

            foreach (EffectPass pass in this.basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Game1.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, triangles, 0, triangles.Length / 3);
            }
        }

        b.End();
        b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState());

        // decor
        {
            position = new Vector2(this.View.Center.X, this.View.Top + this.View.Height * horizon);
            Vector2 offset;
            float ratio;
            foreach (Decor decor in this.Decor)
            {
                ratio = (float)decor.Distance;
                offset = new Vector2(
                    x: (float)perspective(new Vector2((float)decor.Position, (float)decor.Distance), ratio) * this.View.Width,
                    y: ratio * (this.View.Height - this.View.Height * horizon));
                decor.Draw(b, position + offset, scale);
            }
        }

        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState());

        // FOREGROUND

        // bugsplat
        {
            if (this.BugSplat > 0)
            {
                position = new Vector2(this.View.Left + this.View.Width * 0.3f, this.View.Top + this.View.Height * 0.4f);
                source = new(512 + 16 * (int)this.BugSplat, 1696, 16, 16);
                b.Draw(
                    texture: Game1.mouseCursors,
                    position: position + shake,
                    sourceRectangle: source,
                    color: colour,
                    rotation: 0,
                    origin: Vector2.Zero,
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 1);
            }
        }
        // wipers
        {
            position = this.View.Center.ToVector2() + new Vector2(48, 8) * scale;
            source = new(0, 216, 148, 84);
            b.Draw(
                texture: Game.Sprites,
                position: position + shake * 1.25f + this.Shake * 0.5f * scale,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // bus dashboard
        {
            position = this.View.Location.ToVector2();
            source = new(0, 0, 300, 200);
            Vector2 origin = source.Size.ToVector2() / 2;
            Vector2 diff = origin / 2 - this.View.Size.ToVector2() / scale / 2;
            b.Draw(
                texture: Game.Sprites,
                position: position + shake + origin * scale + diff,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: origin,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // engine lights
        {
            if (this.EngineTimer > 0 || this.Failure)
            {
                position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(28, -50) * scale;
                source = new(0, 207, 45, 9);
                b.Draw(
                    texture: Game.Sprites,
                    position: position + shake,
                    sourceRectangle: source,
                    color: colour,
                    rotation: 0,
                    origin: Vector2.Zero,
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 1);
            }
        }
        // mirror
        {
            position = new Vector2(this.View.Center.X, this.View.Top) + new Vector2(60, 22) * scale;
            source = new(0, 300, 80, 32);
            b.Draw(
                texture: Game.Sprites,
                position: position + shake * 1.25f,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // driver photo
        {
            position = new Vector2(this.View.Left, this.View.Top);
            if (this.State.PlayerID != -1)
            {
                Farmer player = Game1.player;
                position += new Vector2(47, 10) * scale;
                player.FarmerRenderer.drawMiniPortrat(b, position + shake, 1, scale, Game1.down, player);
            }
            else if (Game1.getCharacterFromName(this.State.PlayerName) is NPC npc)
            {
                Texture2D texture;
                try
                {
                    texture = Game1.content.Load<Texture2D>($"Characters/{npc.getTextureName()}");
                }
                catch
                {
                    texture = npc.Sprite.Texture;
                }
                position += new Vector2(47, 3) * scale;
                source = npc.getMugShotSourceRect();
                b.Draw(texture, position + shake, source, Color.Lerp(colour, Color.Beige, 0.95f) * 0.85f, 0, Vector2.Zero, scale, SpriteEffects.None, 1);
            }
        }
        // driver name
        {
            string text;
            Vector2 textSize;
            float textScale;
            SpriteFont font = Game1.smallFont;
            const int length = 9;

            text = ModEntry.I18n.Get("game.driver");
            textSize = font.MeasureString(text);
            textScale = scale * 0.2f;
            position = new Vector2(this.View.Left, this.View.Top) + new Vector2(87, 13) * scale;
            Utility.drawBoldText(b, text, font, position + shake - textSize * textScale / 2, Color.Black, textScale);

            text = this.State.PlayerName.ToUpper();
            if (text.Length > length)
                text = $"{text.Take(length)}.";
            textSize = font.MeasureString(text);
            textScale = scale * 0.333f;
            position += new Vector2(0, 8) * scale;
            Utility.drawBoldText(b, text, font, position + shake - textSize * textScale / 2, Color.Black, textScale);
        }
        // chronometer
        {
            position = new Vector2(this.View.Right, this.View.Bottom) + new Vector2(-6, -20) * scale;
            this.Clock.Draw(b, position + shake, scale, alpha);
        }
        // odometer
        {
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(154, -17) * scale;
            this.Odometer.Draw(b, position + shake, scale, alpha);
        }
        // speedometer
        {
            double startRotation = Math.PI * 1.25d; // 6 o'clock
            double addedRotation = this.Speed / 120 * (Math.PI * 2.75d - startRotation); // arbitrary speedo scale magic number to place 70kmh at 1~2 o'clock
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(84, -20) * scale;
            source = new Rectangle(324, 477, 7, 19);
            b.Draw(
                texture: Game1.mouseCursors,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(3.5f, 18f),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // thermometer
        {
            double startRotation = Math.PI * 1.75d;
            double addedRotation = this.FailTimer / this.Rules.FailTime * (Math.PI * 0.5d);
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(34, -16) * scale;
            source = new Rectangle(363, 395, 5, 13);
            b.Draw(
                texture: Game1.mouseCursors,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(2.5f, 12),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // door handle
        {
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(170, -16) * scale
                + new Vector2(this.DoorsTimer * 32, 4 * (float)Math.Sin(Math.PI * this.DoorsTimer)) * scale;
            source = new Rectangle(191, 257, 15, 43);
            b.Draw(
                texture: Game.Sprites,
                position: position + shake * 1.25f,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // steering
        {
            // column
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(50.5f, -4) * scale;
            source = new Rectangle(187, 200, 23, 57);
            b.Draw(
                texture: Game.Sprites,
                position: position + shake * 1.5f,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);

            // wheel
            double wheelRotation = this.WheelRotation;
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(50, -28) * scale;
            source = new Rectangle(210, 200, 90, 90);
            b.Draw(
                texture: Game.Sprites,
                position: position + shake * 2f,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(wheelRotation),
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // scent tree
        {
            const int numFrames = 8;
            int frame = (numFrames + (int)(3 * numFrames * Math.Sin(this.State.Distance / 35d)) % numFrames) / 2;
            position = new Vector2(this.View.Left, this.View.Top) + new Vector2(184, 38) * scale;
            source = new Rectangle(368, 16, 16, 16);
            source.X += source.Width * frame;
            b.Draw(
                texture: Game1.mouseCursors,
                position: position + shake * 1.5f,
                color: colour,
                sourceRectangle: source,
                rotation: 0,
                origin: Vector2.Zero,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // DEBUG: speedometer
        if (ModEntry.Debug)
        {
            int speed = (int)(this.Speed * (ModEntry.Config.Metric ? 1d : 0.621371d));
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(70, -12) * scale;
            Utility.drawTinyDigits(
                toDraw: speed,
                b: b,
                position: position + shake,
                scale: scale,
                layerDepth: 1,
                c: Color.White);
        }
        // DEBUG: odometer
        if (ModEntry.Debug)
        {
            int distance = (int)(this.State.Distance / 1000d * (ModEntry.Config.Metric ? 1d : 0.621371d));
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(106, -12) * scale;
            Utility.drawTinyDigits(
                toDraw: distance,
                b: b,
                position: position + shake,
                scale: scale,
                layerDepth: 1,
                c: Color.White);
        }
        // DEBUG: positionometer
        if (ModEntry.Debug)
        {
            position = new Vector2(this.View.Right, this.View.Bottom) + new Vector2(-16, -16) * scale;
            Utility.drawTextWithShadow(
                b: b,
                text: ((int)this.State.Position).ToString(),
                font: Game1.smallFont,
                position: position + shake,
                color: Color.White,
                shadowIntensity: 0);
        }
        // DEBUG: wheel rotation
        if (ModEntry.Debug)
        {
            string text;
            Vector2 textSize;
            float textScale = scale * 0.25f;
            SpriteFont font = Game1.smallFont;

            text = $"{this.WheelRotation:00}";
            textSize = font.MeasureString(text);
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(16, -64) * scale - new Vector2(textSize.X, 0) * textScale;
            Utility.drawTextWithShadow(
                b: b,
                text: text,
                font: Game1.smallFont,
                position: position + shake,
                color: Color.Black,
                scale: textScale,
                shadowIntensity: 0);

            text = $"{this.WheelSpeed:0.00}";
            textSize = font.MeasureString(text);
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(32, -64) * scale - new Vector2(textSize.X, 0) * textScale;
            Utility.drawTextWithShadow(
                b: b,
                text: text,
                font: Game1.smallFont,
                position: position + shake,
                color: Color.Black,
                scale: textScale,
                shadowIntensity: 0);
        }

        // FADE

        Utility.DrawSquare(b, this.View, 0, null, Color.Black * (1 - this.Opacity));

        // DEBUG: lose condition warning
        //Utility.DrawSquare(b, this.View, 0, null, Color.Black * (float)(this.FailTimer / this.Rules.FailTime) * 0.5f);

        // MASK

        Utility.DrawSquare(b, new(view.Left, view.Top, this.View.Left - view.Left, view.Height), 0, null, Color.Black);
        Utility.DrawSquare(b, new(this.View.Right, view.Top, view.Right - this.View.Right, view.Height), 0, null, Color.Black);
        Utility.DrawSquare(b, new(this.View.Left, view.Top, this.View.Width, this.View.Top - view.Top), 0, null, Color.Black);
        Utility.DrawSquare(b, new(this.View.Left, this.View.Bottom, this.View.Width, view.Bottom - this.View.Bottom), 0, null, Color.Black);

        b.End();
    }

    public bool tick(GameTime time)
    {
        long ticks = time.TotalGameTime.Ticks;
        double ms = time.ElapsedGameTime.TotalMilliseconds;
        double speed = this.Speed;
        bool isOffRoad = this.Rules.Width > 0 && Math.Abs(this.State.Position) > this.Rules.Width / 2;
        bool isOutOfBounds = isOffRoad && Math.Abs(this.State.Position) >= this.Rules.Width;

        // speed
        if (!this.Failure)
        {
            double force = this.Rules.Deceleration * (1 + 2 * this.FailTimer / this.Rules.FailTime);

            if (Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveDownButton))
                force += this.Rules.Braking;
            else if (!isOffRoad && Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveUpButton))
                force += this.Rules.Acceleration * (1 - this.FailTimer / this.Rules.FailTime);

            this.Speed = Math.Clamp(this.Speed + force / ms, 0, this.Rules.MaxSpeed);
            this.State.Distance = Math.Clamp(this.State.Distance + this.Speed / 60d, 0, this.Rules.Distance + this.Rules.MaxSpeed * 10); // a little extra for fade-out
        }
        // steering
        if (!this.Failure)
        {
            // player steering eases in and out to be cool & annoying
            double rotationBounds = Math.PI * this.Rules.SteeringRotations;
            int steeringDirection = Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveRightButton)
                ? 1 : Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveLeftButton)
                    ? -1 : 0;
            double rotation = steeringDirection * this.Rules.SteeringToWheel;
            if (steeringDirection == 0)
                rotation = (Math.Abs(this.WheelSpeed) < Math.Abs(this.Rules.SteeringDecay))
                    ? -this.WheelSpeed // zero-out speed when speed is nearly zero
                    : this.Rules.SteeringDecay * (this.WheelSpeed > 0 ? 1 : -1); // decay speed in opposite direction

            this.WheelSpeed = Math.Clamp(this.WheelSpeed + rotation / ms, -this.Rules.SteeringLimit, this.Rules.SteeringLimit);
            this.WheelRotation = Math.Clamp(this.WheelRotation + this.WheelSpeed / ms, -rotationBounds, rotationBounds);
            // steering gradually returns to zero
            // there are better ways of doing this. i did not do them
            rotation = (Math.Abs(this.WheelRotation) < Math.Abs(this.Rules.SteeringDecay))
                ? -this.WheelRotation // zero-out rotation when rotation is nearly zero
                : this.Rules.SteeringDecay * (this.WheelRotation > 0 ? 1 : -1); // decay rotation in opposite direction
            this.WheelRotation += rotation / ms * 0.5d;

            // complete nonsense
            double fuckYou = Game1.random.NextDouble() * this.Rules.PositionDrift / ms * this.Speed / this.Rules.MaxSpeed;
            this.State.Position = Math.Clamp(this.State.Position + fuckYou + this.WheelRotation * this.Rules.SteeringToGround * this.Speed, -this.Rules.Width * 3, this.Rules.Width * 3);
        }
        // shake
        if (isOffRoad && this.Speed > 0)
        {
            int i = 3;
            if (ticks % 2 == 0)
                this.Shake = new(Game1.random.Next(-i, i), Game1.random.Next(-i, i));
        }
        else
        {
            this.Shake = Vector2.Zero;
        }
        // odometer
        {
            this.Odometer.Update(ms, this.State.Distance);
        }
        // engine
        if (speed <= 0 && this.Speed > 0)
        {
            this.Engine();
        }
        if (this.EngineTimer > 0)
        {
            this.EngineTimer = Math.Max(this.EngineTimer - ms, 0);
        }
        if (Game.RoadNoise is not null)
        {
            double volume = this.Speed / this.Rules.MaxSpeed * 20 + 80;
            Game.RoadNoise.SetVariable("Volume", (int)volume);
        }
        if (Game.EngineNoise is not null)
        {
            double volume = this.Speed / this.Rules.MaxSpeed * 10 + 25;
            Game.EngineNoise.SetVariable("Volume", (int)volume);
            Game.EngineNoise.SetVariable("Frequency", (int)volume);
        }
        // decor
        {
            if (this.Speed > 0 && ticks % 60 == 0 && Game1.random.NextDouble() * 5d * this.Rules.MaxSpeed * 0.9d < this.Speed)
            {
                this.AddDecor();
            }
            foreach (Decor decor in this.Decor)
            {
                decor.Update(ms, this.Speed);
            }
        }
        // doors
        {
            this.DoorsTimer = (float)Math.Clamp(this.DoorsTimer + ms / 250 * this.DoorsOpen, 0, 1);
        }
        // bugsplat
        {
            if (this.State.Distance >= this.Rules.BugSplatDistance)
            {
                if (this.BugSplat == 0)
                    Game1.playSound("slimeHit");
                this.BugSplat = (float)Math.Clamp(this.BugSplat + 1d / ms, 0, 5);
            }
        }
        // lose condition
        {
            double change = isOffRoad ? ms : this.Rules.FailTimeDecay * 1000 / ms;
            this.FailTimer = Math.Clamp(this.FailTimer + change, 0, this.Rules.FailTime);
            if (isOutOfBounds || this.Rules.FailTime > 0 && this.FailTimer >= this.Rules.FailTime)
            {
                // well done!
                //this.Quit = true;
            }
        }
        // fade alpha
        {
            this.Fade = 1;
            if (this.EndGameTimer <= 0)
                this.Fade = -1;
            this.Opacity = (float)Math.Clamp(this.Opacity + this.Fade * 0.25d / ms, 0, 1);
        }
        // end game
        if (this.Failure && this.EndGameTimer > 0)
        {
            this.EndGameTimer = Math.Max(this.EndGameTimer - ms, 0);
        }
        // quit
        if (this.Success || this.Failure)
        {
            // well done!
            this.Quit = true;
        }
        if (this.Quit && this.Opacity <= 0)
        {
            this.unload();

            // now perish
            if (this.Failure)
            {
                Farmer.passOutFromTired(Game1.player);
            }

            Game1.globalFadeToClear();

            return true;
        }
        return false;
    }

    public void unload()
    {
        Game.StartNoise?.Stop(AudioStopOptions.Immediate);
        Game.EngineNoise?.Stop(AudioStopOptions.Immediate);
        Game.RoadNoise?.Stop(AudioStopOptions.Immediate);

        Game1.stopMusicTrack(MusicContext.MiniGame);

        Game1.currentMinigame = null;
        ModEntry.State.Value.Game = null;
    }

    public bool forceQuit()
    {
        this.unload();

        return true;
    }

    public void receiveKeyPress(Keys k)
    {
        // honk
        if (Game1.options.journalButton.Any(key => key.key == k))
        {
            this.Honk();
            return;
        }

        // doors
        if (Game1.options.mapButton.Any(key => key.key == k))
        {
            this.Doors();
            return;
        }

        // quit
        if (k is Keys.Escape)
        {
            this.Quit = true;
            this.EndGameTimer = 0;
            return;
        }
    }

    public void receiveKeyRelease(Keys k)
    {

    }

    public void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.Honk();
    }

    public void receiveRightClick(int x, int y, bool playSound = true)
    {
        this.Doors();
    }

    public void releaseLeftClick(int x, int y)
    {

    }

    public void releaseRightClick(int x, int y)
    {

    }

    public void leftClickHeld(int x, int y)
    {

    }

    public string minigameId()
    {
        return ModEntry.MINIGAME_ID;
    }

    public bool overrideFreeMouseMovement()
    {
        return false;
    }

    public bool doMainGameUpdates()
    {
        return false;
    }

    public void receiveEventPoke(int data)
    {
        throw new NotImplementedException();
    }
}

public class Odometer
{
    public float[] Offsets;
    public double Value;
    public double DisplayValue;
    public double InitialDisplayValue;

    public Odometer(uint digits, double start)
    {
        // values
        this.InitialDisplayValue = start;
        this.Offsets = new float[digits];
        string str = start.ToString();
        for (int i = str.Length - 1; i >= 0; --i)
            this.Offsets[i] = new();
        this.Update(0, 0);
    }

    public void Update(double ms, double value)
    {
        this.Value = value;
        this.DisplayValue = this.InitialDisplayValue + value / 1000d * (ModEntry.Config.Metric ? 1d : 0.621371d);

        // adjust digit offsets
        string digits = ((uint)this.DisplayValue).ToString();
        this.Offsets[digits.Length - 1] = (float)(this.DisplayValue - (int)this.DisplayValue) * -Digits.Slice.Height;
        for (int i = digits.Length - 2; i >= 0; --i)
        {
            uint next = ((uint)digits[i + 1] - '0');
            this.Offsets[i] = next / 10f * -Digits.Slice.Height;
        }
    }

    public void Draw(SpriteBatch b, Vector2 position, float scale, float alpha)
    {
        // DEBUG: backboard
        {
            Vector2 drawSize = new Vector2(Digits.Slice.Width * this.Offsets.Length, Digits.Slice.Height) * scale;
            Vector2 drawPosition = new Vector2(position.X - drawSize.X, position.Y - drawSize.Y);
            Rectangle drawArea = new Rectangle(drawPosition.ToPoint(), drawSize.ToPoint());
            Utility.DrawSquare(b, drawArea, 0, null, Color.Black);

            drawSize = Digits.Slice.Size.ToVector2() * scale;
            drawPosition = new Vector2(position.X - drawSize.X, position.Y - drawSize.Y);
            drawArea = new Rectangle(drawPosition.ToPoint(), drawSize.ToPoint());
            Utility.DrawSquare(b, drawArea, 0, null, Color.White);
        }

        // digits
        string digits = ((uint)this.DisplayValue).ToString();
        for (int i = digits.Length - 1; i >= 0; --i)
        {
            uint digit = (uint)digits[i] - '0';
            uint below = (uint)((digit + 1) % (Digits.Sources.Length));
            float offset = (this.Offsets[i]) * scale;
            Color colour = (i == digits.Length - 1 ? Color.Black : Color.White) * alpha;

            Digits.draw(b, digit, position + new Vector2(0, offset), scale, colour);
            Digits.draw(b, below, position + new Vector2(0, offset + Digits.Slice.Height * scale), scale, colour);
            position -= new Vector2(Digits.Slice.Width * scale, 0);
        }
    }
}

public class Clock
{
    public void Draw(SpriteBatch b, Vector2 position, float scale, float alpha)
    {
        string text = $"{DateTime.Now.Hour:00} {DateTime.Now.Minute:00}";
        Color colour = Color.SpringGreen * alpha;

        // DEBUG: backboard
        {
            Vector2 drawSize = new Vector2(Digits.Slice.Width * text.Length, Digits.Slice.Height) * scale;
            Vector2 drawPosition = new Vector2(position.X - drawSize.X, position.Y - drawSize.Y);
            Rectangle drawArea = new Rectangle(drawPosition.ToPoint(), drawSize.ToPoint());
            Utility.DrawSquare(b, drawArea, 0, null, Color.Black);
        }

        //if (Game1.currentGameTime.TotalGameTime.Seconds % 2 == 0)
        //    return;

        // digits
        for (int i = text.Length - 1; i >= 0; --i)
        {
            uint digit = (uint)text[i] - '0';
            if (digit < Digits.Sources.Length)
                Digits.draw(b, digit, position, scale, colour);
            position -= new Vector2(Digits.Slice.Width * scale, 0);
        }
    }
}

public static class Digits
{
    public static Texture2D Sprites => Game.Sprites;
    public static readonly Rectangle Slice = new Rectangle(x: 0, y: 200, width: 5, height: 7);
    public static readonly Rectangle[] Sources = new Rectangle[10];

    static Digits()
    {
        Rectangle slice = new(location: Point.Zero, size: Digits.Slice.Size);
        for (int i = 0; i < Digits.Sources.Length; ++i)
        {
            Digits.Sources[i] = new Rectangle(
                x: slice.X + Digits.Slice.X,
                y: slice.Y + Digits.Slice.Y,
                width: Digits.Slice.Width,
                height: Digits.Slice.Height);
            slice.X += slice.Width;
        }
    }

    public static void draw(SpriteBatch b, uint digit, Vector2 position, float scale, Color colour)
    {
        b.Draw(
            texture: Digits.Sprites,
            position: position,
            sourceRectangle: Digits.Sources[digit],
            color: colour,
            rotation: 0,
            origin: Utility.PointToVector2(Digits.Slice.Size),
            scale: scale,
            effects: SpriteEffects.None,
            layerDepth: 1);
    }
}

public class Decor : IPooled
{
    public Dictionary<float, Rectangle> Sprites;
    public double Distance;
    public double Position;
    public bool IsOffscreen;

    public bool IsDisposed => this.IsOffscreen;

    public void Reset()
    {
        this.Distance = 0;
        this.Position = 0;
        this.IsOffscreen = false;
    }

    public Decor()
    {
        //(°o,88,o° )/\\\\ aaah a spider
    }

    public void Update(double ms, double speed)
    {
        if (this.IsOffscreen)
            return;

        if (this.Distance >= 0.5d)
            this.IsOffscreen = true;

        this.Distance += speed / ms / 2000d * (1 - Math.Abs(this.Position));
    }

    public void Draw(SpriteBatch b, Vector2 position, float scale)
    {
        if (this.IsOffscreen)
            return;

        Rectangle source = this.Sprites.FirstOrDefault(pair => pair.Key < this.Distance).Value;

        b.Draw(
            texture: Game1.mouseCursors,
            position: position,
            sourceRectangle: source,
            color: Color.White,
            rotation: 0,
            origin: source.Size.ToVector2() / 2,
            scale: scale,
            effects: this.Position < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            layerDepth: (float)this.Distance);
    }
}
