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
    public string From = null;
    public string To = null;
    public int MaxSpeed = 70;
    public double Acceleration = 1.5d;
    public double Deceleration = -0.666d;
    public double Braking = -10.0d;
    public double SteeringLimit = 0.5d;
    public double SteeringToWheel = 0.3d;
    public double SteeringToGround = 0.005d;
    public double SteeringDrift = 5d;
    public double SteeringRotations = 3d;
    public int Width = 100;
    public double Distance = 580000d;
    public int FailTime = 5000;
    public double FailTimeDecay = -0.25d;
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

    public Texture2D Sprites;
    public GameData Data;
    public GameRules Rules;
    public GameState State;

    public Rectangle View;
    public float Opacity;
    public Vector2 Shake;

    public double Speed;
    public double WheelSpeed;
    public double WheelRotation;

    //public double StartTimer;
    //public double DoorTimer;
    public double FailTimer;
    public double EndTimer;

    public bool AllowInput;
    public bool Quit;

    public Game(GameData data, GameRules rules, GameState state)
    {
        Texture2D sprites = ModEntry.Instance.Helper.ModContent.Load<Texture2D>("assets/sprites.png");

        this.Sprites = sprites;
        this.Data = data;
        this.Rules = rules;
        this.State = state;

        this.Opacity = 1;

        this.Speed = 0;
        this.WheelSpeed = 0;
        this.WheelRotation = 0;

        this.FailTimer = 0;
        this.EndTimer = 0;

        this.AllowInput = true;
        this.Quit = false;

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
        Point size = this.Data.Size.ToPoint();
        this.View = new(viewport.X + viewport.Width / 2 - size.X / 2, viewport.Y + viewport.Height / 2 - size.Y / 2, size.X, size.Y);
    }

    public void draw(SpriteBatch b)
    {
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState());

        Rectangle view = Game1.game1.localMultiplayerWindow;
        float scale = 2;// Game1.pixelZoom;
        Vector2 position;
        Rectangle source;
        Vector2 shake = this.Shake + new Vector2(0, 3f * (float)Math.Sin(this.State.Distance / 15d));

        // BACKGROUND

        // sky
        Utility.DrawSquare(b, new(this.View.Left, this.View.Top, this.View.Width, this.View.Height / 3), 0, null, Color.LightSkyBlue);
        Utility.DrawSquare(b, new(this.View.Left, this.View.Top + this.View.Height / 13 * 4, this.View.Width, this.View.Height / 3 * 2), 0, null, Color.LightBlue);
        // sand
        Utility.DrawSquare(b, new(this.View.Left, this.View.Top + this.View.Height / 3, this.View.Width, this.View.Height / 3 * 2), 0, null, Color.Orange);
        // DEBUG: road
        {
            int median = -(int)this.State.Position;
            Utility.drawLineWithScreenCoordinates(this.View.Center.X + median, this.View.Top, this.View.Center.X + median, this.View.Bottom, b, Color.Black, 1, 1);
            //Game1.graphics.GraphicsDevice.DrawUserIndexedPrimitives<>(PrimitiveType.TriangleList, [], 0, 3, [], 0, 1);
        }
        // decorations

        // FOREGROUND

        // driver name
        {
            string text;
            Vector2 textSize;
            float textScale;
            SpriteFont font = Game1.smallFont;

            text = ModEntry.I18n.Get("game.driver");
            textSize = font.MeasureString(text);
            textScale = 0.5f;
            position = new Vector2(this.View.Left + 96, this.View.Top + 24);
            Utility.drawBoldText(b, text, font, position + shake - textSize * textScale / 2, Color.White, textScale);

            text = this.State.PlayerName.ToUpper();
            textSize = font.MeasureString(text);
            textScale = 1f;
            position += new Vector2(0, 24);
            Utility.drawBoldText(b, text, font, position + shake - textSize * textScale / 2, Color.White, textScale);
        }
        // chronometer
        {
            string text = DateTime.Now.ToShortTimeString();
            if (Game1.currentGameTime.TotalGameTime.Seconds % 2 == 0)
                text = text.Replace(':', '.');
            position = new Vector2(this.View.Right - 64, this.View.Bottom - 64);
            Utility.drawTextWithShadow(
                b: b,
                text: text,
                font: Game1.smallFont,
                position: position + shake,
                color: Color.White);
        }
        // DEBUG: speedometer
        {
            int speed = (int)(this.Speed * (ModEntry.Config.Metric ? 1d : 0.621371d));
            position = new Vector2(this.View.Left + 64, this.View.Bottom - 96);
            Utility.drawTinyDigits(
                toDraw: speed,
                b: b,
                position: position + shake,
                scale: scale,
                layerDepth: 1,
                c: Color.White);
        }
        // speedometer
        {
            double startRotation = Math.PI; // 6 o'clock
            double addedRotation = this.Speed / 20; // arbitrary speedo scale magic number to place 70kmh at 1~2 o'clock
            position = new Vector2(this.View.Left + 64, this.View.Bottom - 96);
            source = new Rectangle(363, 395, 5, 13);
            b.Draw(
                texture: Game1.mouseCursors,
                position: position + shake,
                sourceRectangle: source,
                color: Color.White,
                rotation: (float)(startRotation + addedRotation),
                origin: new Vector2(2.5f, 12),
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // odometer
        {
            int distance = (int)(this.State.Distance / 1000d * (ModEntry.Config.Metric ? 1d : 0.621371d));
            position = new Vector2(this.View.Left + 128, this.View.Bottom - 96);
            Utility.drawTinyDigits(
                toDraw: distance,
                b: b,
                position: position + shake,
                scale: scale,
                layerDepth: 1,
                c: Color.White);
        }
        // DEBUG: positionometer
        {
            position = new Vector2(this.View.Left + 256, this.View.Bottom - 96);
            Utility.drawTextWithShadow(
                b: b,
                text: ((int)this.State.Position).ToString(),
                font: Game1.smallFont,
                position: position + shake,
                color: Color.White);
        }
        // wheel
        {
            double wheelRotation = this.WheelRotation;
            position = new Vector2(this.View.Left + this.View.Width / 4, this.View.Bottom - 16);
            source = new Rectangle(228, 465, 37, 37);
            b.Draw(
                texture: Game1.mouseCursors,
                position: position + shake,
                sourceRectangle: source,
                color: Color.White,
                rotation: (float)(wheelRotation),
                origin: source.Size.ToVector2() / 2,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
        }
        // scent tree
        {
            int numFrames = 8;
            int frame = (int)Math.Floor((Math.PI + 2d * Math.Sin(this.State.Distance / 10)) % numFrames);
            position = new Vector2(this.View.Left + this.View.Width / 2, this.View.Top + 64);
            source = new Rectangle(368, 16, 16, 16);
            source.X += source.Width * frame;
            b.Draw(
                texture: Game1.mouseCursors,
                position: position + shake,
                color: Color.White,
                sourceRectangle: source,
                rotation: 0,
                origin: Vector2.Zero,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 1);
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
        {
            double force = this.Rules.Deceleration * (1 + 2 * this.FailTimer / this.Rules.FailTime);

            if (this.AllowInput && Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveDownButton))
                force += this.Rules.Braking;
            else if (this.AllowInput && !isOffRoad && Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveUpButton))
                force += this.Rules.Acceleration * (1 - this.FailTimer / this.Rules.FailTime);

            this.Speed = Math.Clamp(this.Speed + force / ms, 0, this.Rules.MaxSpeed);
            this.State.Distance = Math.Clamp(this.State.Distance + this.Speed / 60d, 0, this.Rules.Distance);
        }
        // steering
        {
            double rotationBounds = Math.PI * this.Rules.SteeringRotations;
            int steeringDirection = Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveRightButton)
                ? 1 : Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.moveLeftButton)
                    ? -1 : 0;
            double fuckYouRotation = Game1.random.NextDouble() / this.Rules.SteeringDrift / ms * this.Speed / this.Rules.MaxSpeed;
            double rotation = steeringDirection * this.Rules.SteeringToWheel + fuckYouRotation;

            this.WheelSpeed = Math.Clamp(this.WheelSpeed + rotation / ms, -this.Rules.SteeringLimit, this.Rules.SteeringLimit);
            this.WheelRotation = Math.Clamp(this.WheelRotation + this.WheelSpeed / ms, -rotationBounds, rotationBounds);

            // complete nonsense
            this.State.Position = Math.Clamp(this.State.Position + this.WheelRotation * this.Rules.SteeringToGround * this.Speed, -this.Rules.Width, this.Rules.Width);
        }
        // shake
        if (isOffRoad && this.Speed > 0)
        {
            int i = 2;
            if (ticks % 2 == 0)
                this.Shake = new(Game1.random.Next(-i, i), Game1.random.Next(-i, i));
        }
        else
        {
            this.Shake = Vector2.Zero;
        }
        // sounds
        if (speed <= 0 && this.Speed > 0 && (Game.StartNoise is null || !Game.StartNoise.IsPlaying))
        {
            Game1.playSound("busDriveOff", out Game.StartNoise);
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
        // win condition
        if (this.Rules.Distance > 0 && this.State.Distance >= this.Rules.Distance)
        {
            // well done!
            this.Quit = true;
        }

        return this.Quit;
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
        if (Game1.options.emoteButton.Any(key => key.key == k))
        {
            Game1.playSound("Duck", pitch: 0);
            return;
        }

        // quit
        if (k is Keys.Escape)
        {
            this.Quit = true;
            return;
        }
    }

    public void receiveKeyRelease(Keys k)
    {

    }

    public void receiveLeftClick(int x, int y, bool playSound = true)
    {

    }

    public void receiveRightClick(int x, int y, bool playSound = true)
    {

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
