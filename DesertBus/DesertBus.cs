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
    public List<GameAudio> Audio;
    public List<GameAppearance> Appearances;
    public List<GameRules> Rules;
}

public class GameAudio
{
    public string Logo;
    public string EngineStart;
    public string EngineLoop;
    public string RoadLoop;
    public string OffroadLoop;
    public string Honk;
    public string DoorOpen;
    public string DoorClose;
    public string BugSplat;
    public string Condition = null;
}

public class GameAppearance
{
    public string Texture;
    public string NightTexture;
    public string LogoTexture;
    public string Condition = null;
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
    public double Braking = -7.5d;
    public double SteeringLimit = 1.0d;
    public double SteeringToWheel = 0.5d;
    public double SteeringToGround = 0.0075d;
    public double SteeringDecay = -0.5d;
    public double SteeringRotations = 3d;
    public double PositionDrift = 5d;
    // others
    public int BugSplatDistance = 362000;
    // road conditions
    public int Width = 100;
    public int Distance = 580000;
    // lose conditions
    public int FailTime = 5000;
    public double FailTimeDecay = -0.1d;
    // rule conditions
    public string Condition = null;
}

public class GameState
{
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
    public static ICue OffroadNoise;

    public Texture2D Logo;
    public Texture2D Sprites;
    public BasicEffect basicEffect;

    public Character Driver;

    public GameData Data;
    public GameAudio Audio;
    public GameAppearance Appearance;
    public GameRules Rules;
    public GameState State;

    public Clock Clock;
    public Odometer Odometer;

    public Pool<Decor> Decor;

    public Rectangle Window => new(0, 0, Game1.viewport.Width, Game1.viewport.Height);

    public Rectangle View;
    public float Opacity;
    public Vector2 Shake;

    public int DoorsOpen;
    public float DoorsTimer;

    public float BugSplat;

    public double Speed;
    public double WheelSpeed;
    public double WheelRotation;

    public bool Brake;
    public bool Accelerator;
    public double AcceleratorTimer;
    public bool EngineOn;
    public double EngineTimer;

    public double LogoTimer;
    public double FailTimer;
    public double EndGameTimer;

    public bool Night;
    public int Fade;
    public bool Quit;

    public bool IsLogoUp => this.LogoTimer > -1000;
    public bool AllowInput => !(this.IsLogoUp || this.Failure);
    public bool CanEnd => this.Success || this.Failure;
    public bool Success => this.Rules.Distance > 0 && this.State.Distance >= this.Rules.Distance;
    public bool Failure => this.Speed <= 0 && this.FailTimer >= this.Rules.FailTime;

    public delegate void OnEndDelegate(bool success);

    public event OnEndDelegate OnEnd;

    public Game(GameData data, GameAudio audio, GameAppearance appearance, GameRules rules, GameState state, GameLocation location, Character driver, double logoTimer = -1000)
    {
        this.Night = Game1.isDarkOut(location);

        this.Audio = audio;
        this.Appearance = appearance;
        Texture2D sprites = ModEntry.Instance.Helper.GameContent.Load<Texture2D>(this.Night ? appearance.NightTexture : appearance.Texture);
        Texture2D logo = ModEntry.Instance.Helper.GameContent.Load<Texture2D>(appearance.LogoTexture);

        this.Sprites = sprites;
        this.Logo = logo;
        this.basicEffect = new(Game1.graphics.GraphicsDevice);

        this.Driver = driver;

        this.Data = data;
        this.Rules = rules;
        this.State = state;

        this.Clock = new();
        uint distance = Game1.player.stats.Get($"{ModEntry.STATS_ID}.Distance");
        this.Odometer = new(digits: 6u, start: ((uint)Math.Floor(601093 + distance / 1000d)));

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

        this.LogoTimer = logoTimer;
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
                {0f, new(144, 300, 16, 48)},
            };
        }

        // noise
        Game1.stopMusicTrack(MusicContext.Default);
        Game1.playSound(this.Audio.RoadLoop, out Game.RoadNoise);
        if (Game.RoadNoise is not null)
        {
            Game.RoadNoise.SetVariable("Volume", 0);
            Game.RoadNoise.Resume();
        }
        Game1.playSound(this.Audio.EngineLoop, out Game.EngineNoise);
        if (Game.EngineNoise is not null)
        {
            Game.EngineNoise.SetVariable("Volume", 0);
            Game.EngineNoise.Resume();
        }
        Game1.playSound(this.Audio.OffroadLoop, out Game.OffroadNoise);
        if (Game.OffroadNoise is not null)
        {
            Game.OffroadNoise.SetVariable("Volume", 0);
            Game.OffroadNoise.Resume();
        }

        if (this.IsLogoUp)
        {
            Game1.playSound(this.Audio.Logo);
        }

        this.changeScreenSize();
    }

    public void changeScreenSize()
    {
        Rectangle window = this.Window;
        Vector2 size = (this.Data.Size * this.Data.Scale / Game1.options.zoomLevel);
        Vector2 position = new Vector2(window.Width - size.X, window.Height - size.Y) / 2;
        this.View = new(position.ToPoint(), size.ToPoint());
    }

    public void AddDecor(bool randomY = false)
    {
        Decor decor = this.Decor.Get();
        decor.Position = Game1.random.NextDouble() - 0.5d;
        decor.Distance = randomY ? Game1.random.NextDouble() * 0.25d : 0d;
        if (0d < decor.Position && decor.Position < 0.01d && this.State.Distance % 25 < 1)
        {
            // sign
            decor.Sprites = new Dictionary<float, Rectangle>{
                {0.25f, new(144, 300, 16, 48)},
                {0.15f, new(128, 300, 16, 48)},
                {0.075f, new(112, 300, 16, 48)},
                {0.025f, new(96, 300, 16, 48)},
                {0f, new(80, 300, 16, 48)},
            };
        }
        else if (Game1.random.NextDouble() < 0.01d)
        {
            // rare bits
            Dictionary<float, Rectangle>[] sprites = [
                // jones bones
                new Dictionary<float, Rectangle>{
                    {0.25f, new(272, 316, 16, 16)},
                    {0.125f, new(256, 316, 16, 16)},
                    {0.05f, new(240, 316, 16, 16)},
                    {0f, new(224, 316, 16, 16)},
                },
                // crazy castle
                new Dictionary<float, Rectangle>{
                    {0.25f, new(256, 380, 32, 32)},
                    {0.125f, new(224, 380, 32, 32)},
                    {0.05f, new(192, 380, 32, 32)},
                    {0f, new(160, 380, 32, 32)},
                },
            ];
            decor.Sprites = sprites[Game1.random.Next(sprites.Length)];
        }
        else if (Game1.random.NextDouble() < 0.2d)
        {
            // greenery
            Dictionary<float, Rectangle>[] sprites = [
                // cactus
                new Dictionary<float, Rectangle>{
                    {0.25f, new(144, 348, 16, 32)},
                    {0.15f, new(128, 348, 16, 32)},
                    {0.075f, new(112, 348, 16, 32)},
                    {0.025f, new(96, 348, 16, 32)},
                    {0f, new(80, 348, 16, 32)},
                },
                // prickly pear
                new Dictionary<float, Rectangle>{
                    {0.25f, new(272, 300, 16, 16)},
                    {0.125f, new(256, 300, 16, 16)},
                    {0.05f, new(240, 300, 16, 16)},
                    {0f, new(224, 300, 16, 16)},
                },
                // just some grass
                new Dictionary<float, Rectangle>{
                    {0.25f, new(64, 332, 16, 16)},
                    {0.15f, new(48, 332, 16, 16)},
                    {0.075f, new(32, 332, 16, 16)},
                    {0.05f, new(16, 332, 16, 16)},
                    {0f, new(0, 332, 16, 16)},
                },
            ];
            decor.Sprites = sprites[Game1.random.Next(sprites.Length)];
        }
        else
        {
            // brush
            Dictionary<float, Rectangle>[] sprites = [
                new Dictionary<float, Rectangle>{
                    {0.25f, new(208, 300, 16, 16)},
                    {0.125f, new(192, 300, 16, 16)},
                    {0.05f, new(176, 300, 16, 16)},
                    {0f, new(160, 300, 16, 16)},
                },
                new Dictionary<float, Rectangle>{
                    {0.25f, new(208, 316, 16, 16)},
                    {0.125f, new(192, 316, 16, 16)},
                    {0.05f, new(176, 316, 16, 16)},
                    {0f, new(160, 316, 16, 16)},
                },
                new Dictionary<float, Rectangle>{
                    {0.25f, new(208, 332, 16, 24)},
                    {0.125f, new(192, 332, 16, 24)},
                    {0.05f, new(176, 332, 16, 24)},
                    {0f, new(160, 332, 16, 24)},
                },
                new Dictionary<float, Rectangle>{
                    {0.25f, new(208, 356, 16, 24)},
                    {0.125f, new(192, 356, 16, 24)},
                    {0.05f, new(176, 356, 16, 24)},
                    {0f, new(160, 356, 16, 24)},
                }
            ];
            decor.Sprites = sprites[Game1.random.Next(sprites.Length)];
        }
    }

    public void Engine()
    {
        if (!this.EngineOn)
        {
            this.EngineOn = true;
            this.EngineTimer = 2000;
            if (Game.StartNoise is null || !Game.StartNoise.IsPlaying)
            {
                Game1.playSound(this.Audio.EngineStart, out Game.StartNoise);
            }
        }
    }

    public void Honk()
    {
        Game1.playSound(this.Audio.Honk, pitch: 0);
    }

    public void Doors()
    {
        if (this.DoorsOpen > 0)
            Game1.playSound(this.Audio.DoorOpen);
        else
            Game1.playSound(this.Audio.DoorClose);
        this.DoorsOpen *= -1;
    }

    public void draw(SpriteBatch b)
    {
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState());
        
        Rectangle view = this.Window;
        float scale = this.Data.Scale / Game1.options.zoomLevel;
        Vector2 position;
        Rectangle source;
        Vector2 shake = this.Shake
            + new Vector2(0, 3f * (float)Math.Sin(this.State.Distance / 20d)) * scale
            + new Vector2((float)(Math.Min(100, Math.Abs(this.State.Position)) * Math.Sign(this.State.Position) / 20d), 0) * scale;
        Color colour = Color.White;
        float alpha = 1f;
        Texture2D texture = this.Sprites;

        // LOGO

        if (this.LogoTimer > 0)
        {
            position = this.View.Center.ToVector2();
            b.Draw(
                texture: this.Logo,
                position: position,
                sourceRectangle: null,
                color: Color.White,
                rotation: 0,
                origin: this.Logo.Bounds.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);

            b.End();
            return;
        }

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
        {
            position = this.View.Center.ToVector2();
            source = new(0, 420, 270, 180);
            b.Draw(
                texture: texture,
                position: position + new Vector2(median, 0) * scale,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
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

            Vector3[] vertices = [];
            VertexPositionColor[] triangles = [
                new (viewToVertex(left), this.Night ? new(10,5,10) : new(95, 90, 95)),
                new (viewToVertex(top), this.Night ? new(2,1,2) : new Color(105, 95, 85) * 0.975f),
                new (viewToVertex(right), this.Night ? new(10,5,10) : new(95, 90, 95)),
            ];

            const int num = 12;
            for (int i = 0; i < num; ++i)
            {
                double value = this.State.Distance / 3;
                double rate = 100;
                double d = (double)i / num;
                double t = (value + d * rate) % rate / rate;
                float ratio = (float)Math.Pow(t, 2);
                Vector2 size = new(0.025f, 0.019f);
                Color stripeColour = this.Night ? Color.Lerp(Color.Black, new Color(245, 185, 0), ratio * ratio) : new Color(245, 185, 0);

                float ratioT = Math.Max(0, ratio - size.Y * 4 * ratio) * ratio;
                float ratioB = Math.Max(0, ratio + size.Y * 4 * ratio) * ratio;

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
                triangles = triangles.Concat(vertices.Select(v => new VertexPositionColor(v, stripeColour * 0.975f))).ToArray();
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
                decor.Draw(b, texture, position + offset, scale);
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
                    color: this.Night ? Color.Lerp(colour, Color.Black, 0.825f) : colour,
                    rotation: 0,
                    origin: Vector2.Zero,
                    scale: scale,
                    effects: SpriteEffects.None,
                    layerDepth: 1);
            }
        }
        // wipers
        {
            position = this.View.Center.ToVector2() + new Vector2(52, 4) * scale;
            source = new(0, 216, 172, 84);
            b.Draw(
                texture: texture,
                position: position + shake * 1.25f + this.Shake * 0.5f * scale,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // odometer
        {
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(165, -23) * scale;
            this.Odometer.Draw(b, texture, position + shake, scale, alpha, this.Night ? new(75,85,75) : Color.White);
        }
        // bus dashboard
        {
            position = this.View.Center.ToVector2() + new Vector2(19, 0) * scale;
            source = new(0, 0, 300, 200);
            b.Draw(
                texture: texture,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // engine lights
        {
            if (this.EngineTimer > 0 || this.Failure)
            {
                position = new Vector2(this.View.Left, this.View.Bottom);
                List<(Vector2, Rectangle)> lights = [
                    (new(-4, -22), new(0, 207, 9, 9)), // battery on revometer
                    (new(27, -20), new(9, 207, 9, 9)), // fuel on fullo'fuelometer
                    (new(93, -22), new(18, 207, 9, 9)), // engine on speedometer
                    //(new(0, 0), new(27, 207, 9, 9)), // oil on ????
                    (new(63, -20), new(35, 207, 9, 9)), // temp on thermometer
                ];
                foreach ((Vector2 v, Rectangle r) in lights)
                {
                    b.Draw(
                        texture: texture,
                        position: position + v * scale + shake,
                        sourceRectangle: r,
                        color: colour * 0.75f,
                        rotation: 0,
                        origin: Vector2.Zero,
                        scale: scale,
                        effects: SpriteEffects.None,
                        layerDepth: 1);
                }
            }
        }
        // scent tree
        {
            const int numFrames = 5;
            int frame = numFrames - 1 - ((int)(numFrames * Math.Abs(Math.Sin(this.State.Distance / 50d))));
            position = new Vector2(this.View.Left, this.View.Top) + new Vector2(182, 50) * scale;
            source = new Rectangle(0, 380, 24, 40);
            source.X += source.Width * frame;
            b.Draw(
                texture: texture,
                position: position + shake * 1.5f,
                color: colour,
                sourceRectangle: source,
                rotation: 0,
                origin: Vector2.Zero,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // mirror
        {
            position = new Vector2(this.View.Center.X, this.View.Top) + new Vector2(60, 37) * scale;
            source = new(0, 300, 80, 32);
            b.Draw(
                texture: texture,
                position: position + shake * 1.125f,
                sourceRectangle: source,
                color: colour,
                rotation: 0,
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);

            // you!!!
            if (this.Driver is not Farmer)
            {
                Farmer player = Game1.player;
                position += new Vector2(16, -3) * scale;
                player.FarmerRenderer.drawMiniPortrat(b, position + shake * 1.125f, 1, scale, Game1.down, player, alpha: 0.75f);
            }
        }
        // driver photo
        {
            position = new Vector2(this.View.Left, this.View.Top);
            if (this.Driver is Farmer player)
            {
                position += new Vector2(51, 12) * scale;
                player.FarmerRenderer.drawMiniPortrat(b, position + shake, 1, scale, Game1.down, player, this.Night ? 0.25f : 0.8f);
            }
            else if (this.Driver is NPC npc)
            {
                Texture2D npcTexture;
                try
                {
                    npcTexture = Game1.content.Load<Texture2D>($"Characters/{npc.getTextureName()}");
                }
                catch
                {
                    npcTexture = npc.Sprite.Texture;
                }
                position += new Vector2(51, 3) * scale;
                source = npc.getMugShotSourceRect();
                b.Draw(npcTexture, position + shake, source, Color.Lerp(colour, Color.Beige, 0.95f) * 0.85f, 0, Vector2.Zero, scale, SpriteEffects.None, 1);
            }
        }
        // driver name
        {
            string text;
            Vector2 textSize;
            float textScale;
            Color textColour = this.Night ? new(12,7,12) : new(50,15,30);
            SpriteFont font = Game1.smallFont;
            const int length = 9;

            text = "YOUR DRIVER IS";
            textSize = font.MeasureString(text);
            textScale = scale * 0.2f;
            position = new Vector2(this.View.Left, this.View.Top) + new Vector2(91, 12) * scale;
            Utility.drawBoldText(b, text, font, position + shake - textSize * textScale / 2, textColour, textScale);

            text = this.Driver.displayName.ToUpper();
            if (text.Length > length)
                text = $"{text.Take(length)}.";
            textSize = font.MeasureString(text);
            textScale = scale * 0.333f;
            position += new Vector2(0, 10) * scale;
            Utility.drawBoldText(b, text, font, position + shake - textSize * textScale / 2, textColour, textScale);
        }
        // chronometer
        {
            position = new Vector2(this.View.Center.X, this.View.Top) + new Vector2(72, 11) * scale;
            this.Clock.Draw(b, texture, position + shake, scale, alpha);
        }
        // speedometer
        {
            double startRotation = Math.PI * 1.25d; // 7:30 o'clock
            double addedRotation = this.Speed / 100d * (Math.PI * 2.75d - startRotation); // arbitrary speedo scale magic number for speed 0~100 at 7:30~4:30 o'clock respectively
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(98.5f, -27f) * scale;
            source = new Rectangle(62, 200, 7, 16);
            b.Draw(
                texture: texture,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(4f, 14f),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // revometer
        {
            double startRotation = Math.PI * 1.25d; // 6 o'clock
            double addedRotation = Math.Max(0, this.Speed / 100d * Math.PI * 0.5d // increase with speed
                + 0.75d * Math.Abs(Math.Sin(this.Speed / 10)) * Math.PI // pretend we're changing gears
                - this.FailTimer / this.Rules.FailTime * Math.PI / 5d) // choke on dirt
                 * this.AcceleratorTimer; // only spool up when accelerating
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(1.5f, -27f) * scale;
            source = new Rectangle(62, 200, 7, 16);
            b.Draw(
                texture: texture,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(4f, 14f),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // thermometer
        {
            double startRotation = Math.PI * 1.75d;
            double addedRotation = this.FailTimer / this.Rules.FailTime * (Math.PI * 0.5d);
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(68, -4) * scale;
            source = new Rectangle(55, 200, 7, 16);
            b.Draw(
                texture: texture,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(source.Width / 2f, source.Height - 3),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // fullo'fuelometer
        {
            double startRotation = Math.PI * 2.1d;
            double addedRotation = Math.Sin(this.State.Distance / 2000) / Math.PI / 2;
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(30, -4) * scale;
            source = new Rectangle(55, 200, 7, 16);
            b.Draw(
                texture: texture,
                position: position + shake,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(source.Width / 2f, source.Height - 3),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // door handle
        {
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(176, -12) * scale
                + new Vector2(this.DoorsTimer * 32, 4 * (float)Math.Sin(Math.PI * this.DoorsTimer)) * scale;
            source = new Rectangle(176, 257, 15, 43);
            b.Draw(
                texture: texture,
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
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(47, -6) * scale;
            source = new Rectangle(172, 200, 23, 57);
            b.Draw(
                texture: texture,
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
            position = new Vector2(this.View.Left, this.View.Bottom) + new Vector2(47, -30) * scale;
            source = new Rectangle(200, 200, 100, 100);
            b.Draw(
                texture: texture,
                position: position + shake * 2f,
                sourceRectangle: source,
                color: colour,
                rotation: (float)(wheelRotation),
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }

        // FADE

        Utility.DrawSquare(b, this.View, 0, null, Color.Black * (1 - this.Opacity));

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

        // logo
        if (this.IsLogoUp)
        {
            this.LogoTimer -= ms;
            return this.Quit;
        }
        // input
        {
            this.Brake = this.AllowInput && Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveDownButton);
            this.Accelerator = this.AllowInput && Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveUpButton);
        }
        // speed
        if (!this.Failure)
        {
            double force = this.Rules.Deceleration * (1 + 2 * this.FailTimer / this.Rules.FailTime);

            if (this.Brake)
                force += this.Rules.Braking;
            else if (!isOffRoad && this.Accelerator)
                force += this.Rules.Acceleration * (1 - this.FailTimer / this.Rules.FailTime);

            this.Speed = Math.Clamp(this.Speed + force / ms, 0, this.Rules.MaxSpeed);
            this.State.Distance = Math.Clamp(this.State.Distance + this.Speed / 60d, 0, this.Rules.Distance + this.Rules.MaxSpeed * 10); // a little extra for fade-out
        }
        // steering
        if (this.AllowInput)
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
        else if (this.Speed < 0.01d && this.EngineOn)
        {
            int i = 3;
            if (ticks % 2 == 0)
                this.Shake = new Vector2(Game1.random.Next(-i, i), Game1.random.Next(-i, i))
                    * (float)(0.1f * Math.Sin(Math.PI + Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 500));
        }
        else
        {
            this.Shake = Vector2.Zero;
        }
        // accelerator (fake)
        {
            this.AcceleratorTimer = Math.Clamp(this.AcceleratorTimer + 1d / ms / 100d * this.Rules.Acceleration * (this.Accelerator ? 1 : -3), 0d, 1d);
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
        if (this.Failure)
        {
            this.EngineOn = false;
        }
        if (this.EngineTimer > 0)
        {
            this.EngineTimer = Math.Max(this.EngineTimer - ms, 0);
        }
        if (Game.RoadNoise is not null)
        {
            double volume = this.Speed / this.Rules.MaxSpeed * this.AcceleratorTimer * 20 + 80;
            Game.RoadNoise.SetVariable("Volume", (int)volume);
        }
        if (Game.EngineNoise is not null)
        {
            double volume = this.Speed / this.Rules.MaxSpeed * 10 + 25;
            Game.EngineNoise.SetVariable("Volume", (int)volume);
            Game.EngineNoise.SetVariable("Frequency", (int)volume);
        }
        if (Game.OffroadNoise is not null)
        {
            double volume = isOffRoad ? 100 : 0;
            Game.OffroadNoise.SetVariable("Volume", (int)volume);
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
                    Game1.playSound(this.Audio.BugSplat);
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
        if (this.CanEnd && this.EndGameTimer > 0)
        {
            this.EndGameTimer = Math.Max(this.EndGameTimer - ms, 0);
        }
        // quit
        if (this.CanEnd)
        {
            // well done!
            this.Quit = true;
        }
        if (this.Quit && this.Opacity <= 0)
        {
            this.unload();

            this.OnEnd?.Invoke(this.Success);

            Game1.globalFadeToClear();

            // update player stats
            Game1.player.stats.Increment($"{ModEntry.STATS_ID}.Distance", (uint)this.State.Distance);
            Game1.player.stats.Set($"{ModEntry.STATS_ID}.MaxDistance", Math.Max(Game1.player.stats.Get($"{ModEntry.STATS_ID}.MaxDistance"), (uint)this.State.Distance));
            if (this.Success)
            {
                Game1.player.stats.Increment($"{ModEntry.STATS_ID}.Success");
            }
            else if (this.Failure)
            {
                Game1.player.stats.Increment($"{ModEntry.STATS_ID}.Failure");
            }

            return true;
        }
        return false;
    }

    public void unload()
    {
        Game.StartNoise?.Stop(AudioStopOptions.Immediate);
        Game.EngineNoise?.Stop(AudioStopOptions.Immediate);
        Game.RoadNoise?.Stop(AudioStopOptions.Immediate);
        Game.OffroadNoise?.Stop(AudioStopOptions.Immediate);

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
        if (this.AllowInput)
        {
            this.Honk();
        }
    }

    public void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (this.AllowInput)
        {
            this.Doors();
        }
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

    public Odometer(uint digits, uint start)
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
        this.DisplayValue = this.InitialDisplayValue + value / 1000d;

        // adjust digit offsets
        string digits = ((uint)this.DisplayValue).ToString();
        this.Offsets[digits.Length - 1] = (float)(this.DisplayValue - (int)this.DisplayValue) * -Digits.Slice.Height;
        for (int i = digits.Length - 2; i >= 0; --i)
        {
            uint next = ((uint)digits[i + 1] - '0');
            this.Offsets[i] = next / 10f * -Digits.Slice.Height;
        }
    }

    public void Draw(SpriteBatch b, Texture2D texture, Vector2 position, float scale, float alpha, Color colour)
    {
        // backboard
        {
            Vector2 drawSize = new Vector2(Digits.Slice.Width * this.Offsets.Length, Digits.Slice.Height) * scale;
            Vector2 drawPosition = new Vector2(position.X - drawSize.X, position.Y - drawSize.Y);
            Rectangle drawArea = new Rectangle(drawPosition.ToPoint(), drawSize.ToPoint());
            drawArea.Inflate(1 * scale, 1 * scale); // this line DESTROYS float arithmetic gaps
            Utility.DrawSquare(b, drawArea, 0, null, Color.Black);

            drawSize = Digits.Slice.Size.ToVector2() * scale;
            drawPosition = new Vector2(position.X - drawSize.X, position.Y - drawSize.Y);
            drawArea = new Rectangle(drawPosition.ToPoint(), drawSize.ToPoint());
            Utility.DrawSquare(b, drawArea, 0, null, colour);
        }

        // digits
        string digits = ((uint)this.DisplayValue).ToString();
        for (int i = digits.Length - 1; i >= 0; --i)
        {
            uint digit = (uint)digits[i] - '0';
            uint below = ((digit + 1) % 10);
            float offset = (this.Offsets[i]) * scale;
            Color digitColour = (i == digits.Length - 1 ? Color.Black : Color.White) * alpha;

            Digits.draw(b, texture, digit, position + new Vector2(0, offset), scale, digitColour);
            Digits.draw(b, texture, below, position + new Vector2(0, offset + Digits.Slice.Height * scale), scale, digitColour);
            position -= new Vector2(Digits.Slice.Width * scale, 0);
        }
    }
}

public class Clock
{
    public void Draw(SpriteBatch b, Texture2D texture, Vector2 position, float scale, float alpha)
    {
        char c = Game1.currentGameTime.TotalGameTime.Seconds % 2 == 0 ? ' ' : ':';
        string text = $"{DateTime.Now.Hour:00}{c}{DateTime.Now.Minute:00}";
        Color colour = Color.SpringGreen * 0.95f * alpha;

        // backboard
        {
            Vector2 drawSize = new Vector2(Digits.Slice.Width * text.Length, Digits.Slice.Height) * scale;
            Vector2 drawPosition = new Vector2(position.X - drawSize.X, position.Y - drawSize.Y);
            Rectangle drawArea = new Rectangle(drawPosition.ToPoint(), drawSize.ToPoint());
            Utility.DrawSquare(b, drawArea, 0, null, Color.Black);
        }

        // digits
        for (int i = text.Length - 1; i >= 0; --i)
        {
            uint digit = (uint)text[i] - '0';
            Digits.draw(b, texture, digit, position, scale, colour);
            position -= new Vector2(Digits.Slice.Width * scale, 0);
        }
    }
}

public static class Digits
{
    public static readonly Rectangle Slice = new Rectangle(x: 0, y: 200, width: 5, height: 7);
    public static readonly Rectangle[] Sources = new Rectangle[11];

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

    public static void draw(SpriteBatch b, Texture2D texture, uint digit, Vector2 position, float scale, Color colour)
    {
        if (digit < 0 || digit >= Digits.Sources.Length)
            return;
        b.Draw(
            texture: texture,
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

        if (this.Distance >= 0.666d)
            this.IsOffscreen = true;

        this.Distance += speed / ms / 2000d * (1 - Math.Abs(this.Position));
    }

    public void Draw(SpriteBatch b, Texture2D texture, Vector2 position, float scale)
    {
        if (this.IsOffscreen)
            return;

        Rectangle source = this.Sprites.FirstOrDefault(pair => pair.Key < this.Distance).Value;

        b.Draw(
            texture: texture,
            position: position,
            sourceRectangle: source,
            color: Color.White * (float)(this.Distance * 50),
            rotation: 0,
            origin: source.Size.ToVector2() / 2,
            scale: scale,
            effects: this.Position < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            layerDepth: (float)this.Distance);
    }
}
