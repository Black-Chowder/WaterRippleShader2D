using BlackMagic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace WaterTest
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        //Misc Water Parameters/Variables
        private const float Damping = 0.99f;
        private const int BrushSize = 1;

        //CPU Implementation variables
        private float[,] prevData;
        private float[,] curData;
        private float cpuTimeAccumulator = 0f;
        private RenderTarget2D cpuRt;

        //Shader implementation variables
        private Effect WaterEffect;
        private RenderTarget2D prevRt;
        private RenderTarget2D curRt;
        private RenderTarget2D newFrame;
        private float shaderTimeAccumulator = 0f;

        //Define screen size
        private Point iResolution = new Point(100, 100); //This is the size the effects will be rendered at
        private const int ResolutionScaler = 8; //This is the scale at which the effects are being blown up to fill the screen

        //Time manipulation variables
        private Point prevMousePos;
        private float timeMod = 0f;
        private const float timeStep = 16f;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            //Set window size
            graphics.PreferredBackBufferWidth = iResolution.X * 2 * ResolutionScaler;
            graphics.PreferredBackBufferHeight = iResolution.Y * ResolutionScaler;
            graphics.ApplyChanges();
        }

        protected override void Initialize()
        {
            //Initialize data arrays for CPU implementation
            prevData = new float[iResolution.X, iResolution.Y];
            curData = new float[iResolution.X, iResolution.Y];
            cpuRt = new RenderTarget2D(GraphicsDevice, iResolution.X, iResolution.Y);

            //Initialize render targets for shader implementation
            curRt = new RenderTarget2D(GraphicsDevice, iResolution.X, iResolution.Y);
            prevRt = new RenderTarget2D(GraphicsDevice, iResolution.X, iResolution.Y);
            newFrame = curRt;

            prevMousePos = Mouse.GetState().Position;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            //Load main water shader
            WaterEffect = Content.Load<Effect>(@"WaterEffect");
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            //Get mouse input
            MouseState mouseState = Mouse.GetState();
            Point mousePos = mouseState.Position;
            bool isMousePressed = mouseState.LeftButton == ButtonState.Pressed;

            //Handle time manipulation
            timeMod = (prevMousePos - mousePos).ToVector2().Length();
            prevMousePos = mousePos;

            if (isMousePressed)
            {
                //Handle input for CPU-based effect
                for (int x = 0; x < BrushSize && mousePos.X / ResolutionScaler + x < iResolution.X; x++)
                {
                    for (int y = 0; y < BrushSize && mousePos.Y / ResolutionScaler + y < iResolution.Y; y++)
                    {
                        prevData[(mousePos.X / ResolutionScaler) + x, (mousePos.Y / ResolutionScaler) + y] = 1.0f;
                    }
                }

                //Handle input for shader-based effect
                RenderTarget2D newPrev = new RenderTarget2D(GraphicsDevice, iResolution.X, iResolution.Y);
                GraphicsDevice.SetRenderTarget(newPrev);
                spriteBatch.Begin();
                spriteBatch.Draw(prevRt, Vector2.Zero, Color.White);
                spriteBatch.Draw(GetTexture(),
                    new Rectangle(mousePos.X / ResolutionScaler, mousePos.Y / ResolutionScaler, BrushSize, BrushSize),
                    Color.White);
                spriteBatch.End();
                GraphicsDevice.SetRenderTarget(null);

                prevRt.Dispose();
                prevRt = newPrev;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            UpdateCPU();
            UpdateShader();

            //Output Results To Screen
            GraphicsDevice.SetRenderTarget(null);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            //CPU-based
            spriteBatch.Draw(cpuRt,
                new Rectangle(0, 0, iResolution.X * ResolutionScaler, iResolution.Y * ResolutionScaler),
                Color.White);

            //Shader-based
            spriteBatch.Draw(newFrame,
                new Rectangle(iResolution.X * ResolutionScaler, 0, iResolution.X * ResolutionScaler, iResolution.Y * ResolutionScaler),
                Color.White);

            spriteBatch.End();


            base.Draw(gameTime);
        }

        private void UpdateCPU()
        {
            for (cpuTimeAccumulator += timeMod; cpuTimeAccumulator > timeStep; cpuTimeAccumulator -= timeStep)
            {                
                //Update curData and prevData and set colorData accordingly
                Color[] colorData = new Color[iResolution.X * iResolution.Y];
                for (int i = 1; i < iResolution.X - 1; i++)
                {
                    for (int j = 1; j < iResolution.Y - 1; j++)
                    {
                        curData[i, j] = (
                            prevData[i - 1, j] + prevData[i + 1, j] + prevData[i, j - 1] + prevData[i, j + 1]) / 2 - curData[i, j];

                        curData[i, j] *= Damping;

                        int index = (i + j * iResolution.X);
                        colorData[index] = new Color(curData[i, j], curData[i, j], curData[i, j], 1.0f);
                    }
                }

                //Swap
                (curData, prevData) = (prevData, curData);

                //Assign colorData to output render target
                cpuRt.SetData(colorData);
            }

        }

        private void UpdateShader()
        {
            shaderTimeAccumulator += timeMod;

            //When not time to itterate effect, render new frame without swapping or modifying previous or current render targets
            if (shaderTimeAccumulator <= timeStep)
            {
                //Set effect parameters
                WaterEffect.Parameters["iResolution"].SetValue(iResolution.ToVector2());
                WaterEffect.Parameters["damping"].SetValue(Damping);
                WaterEffect.Parameters["Previous"].SetValue(prevRt);

                //Calculate new frame
                newFrame = new RenderTarget2D(GraphicsDevice, iResolution.X, iResolution.Y);
                GraphicsDevice.SetRenderTarget(newFrame);
                GraphicsDevice.Clear(Color.Black);
                spriteBatch.Begin(effect: WaterEffect);
                spriteBatch.Draw(curRt, Vector2.Zero, Color.White);
                spriteBatch.End();
            }

            //Normal itteration effect with current render target assignment and render target swapping6
            for (; shaderTimeAccumulator > timeStep; shaderTimeAccumulator -= timeStep)
            {
                //Set effect parameters
                WaterEffect.Parameters["iResolution"].SetValue(iResolution.ToVector2());
                WaterEffect.Parameters["damping"].SetValue(Damping);
                WaterEffect.Parameters["Previous"].SetValue(prevRt);

                //Calculate new frame
                newFrame = new RenderTarget2D(GraphicsDevice, iResolution.X, iResolution.Y);
                GraphicsDevice.SetRenderTarget(newFrame);
                GraphicsDevice.Clear(Color.Black);
                spriteBatch.Begin(effect: WaterEffect);
                spriteBatch.Draw(curRt, Vector2.Zero, Color.White);
                spriteBatch.End();

                //Set current render target to new frame
                curRt.Dispose();
                curRt = newFrame;

                //Swap
                (curRt, prevRt) = (prevRt, curRt);
            }
        }

        //Helper function to create quick texture
        private Texture2D texture;
        private Texture2D GetTexture()
        {
            if (texture != null) return texture;
            texture = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            texture.SetData<Color>(new Color[] { Color.White });
            return texture;
        }
    }
}
