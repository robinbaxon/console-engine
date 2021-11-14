using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Microsoft.Xna.Framework;
using TerraForM.Commands;
using TerraForM.GameObjects;

namespace TerraForM.Scenes
{
    public class GameScene : Scene<TerraformGame>
    {
        //**********************************************************
        //** fields
        //**********************************************************

        private const string MapAssetPath = "Assets/Maps";
        private readonly string _worldName;
        private readonly Queue<Command> _currentCommands = new();
        private readonly List<PlantEmitter> _plantEmitters = new();
              
        //**********************************************************
        //** ctor
        //**********************************************************
        
        public GameScene(string worldName)
        {
            if (worldName == null) throw new ArgumentNullException(nameof(worldName));
            _worldName = worldName;
        }
              
        //**********************************************************
        //** props:
        //**********************************************************

        public World World { get; private set; }
        public Rover Rover { get; private set; }
        public Hud Hud { get; private set; }
        public Camera Camera { get; private set; }
        public bool GameOver { get; set; }
      
        //**********************************************************
        //** public methods:
        //**********************************************************
        
        public override void OnLoad()
        {
            Camera = new Camera(Game);
            World = WorldLoader.LoadWorld(Game, $"{MapAssetPath}/{_worldName}.txt");
            Rover = new Rover(Game)
            {
                Position = World.StartingPoint.Position,
                Velocity = Vector2.Zero
            };
            Hud = new Hud(Game);
            Camera.Follow(Rover);
        }

        public override void OnUnload()
        {
            World = null;
            Rover = null;
            _currentCommands.Clear();
            _plantEmitters.Clear();
        }

        public override void OnUpdate()
        {
            GameOver = Rover.PowerDepleted() || Rover.RemainingSequences < 0;
            
            Hud.OnUpdate();
            
            if (Input.Instance.GetKey(Key.ESCAPE).Held) 
                Game.Scenes.Push<MenuScene>();

            HandleDebugInput();

            if (Input.Instance.GetKey(Key.R).Pressed) 
                Game.Scenes.Reload();
            
            if (Input.Instance.GetKey(Key.SPACE).Pressed && !_currentCommands.Any()) 
                CommitCommands();
            
            UpdateCurrentCommand();
            
            Rover.Update();
            _plantEmitters.ForEach(e => e.Update());
            Camera.Update();
        }
        
        private void HandleDebugInput()
        {
            if (!Game.IsDebugMode) return;
            if (Input.Instance.GetKey(Key.A).Held) Rover.MoveWest();
            if (Input.Instance.GetKey(Key.D).Held) Rover.MoveEast();
            if (Input.Instance.GetKey(Key.W).Held) Rover.MoveNorth();
            if (Input.Instance.GetKey(Key.S).Held) Rover.MoveSouth();
        }

        private void CommitCommands()
        {
            var commands = Hud.GetCommands().ToList();
            if (commands.Any())
            {
                Rover.RemainingSequences--;
            }

            foreach (var command in commands)
                _currentCommands.Enqueue(command);
        }
        
        private void UpdateCurrentCommand()
        {
            if (!_currentCommands.Any()) return;
            
            var currentCommand = _currentCommands.Peek();
            currentCommand.Update(Rover);
            if (currentCommand.IsDone())
            {
                _currentCommands.Dequeue();
            }
        }

        public override void OnRender()
        {
            _plantEmitters.ForEach(e => e.Draw());
            Rover.Draw();
            Hud.Draw();
            
            RenderDebugInfo();
        }

        private void RenderDebugInfo()
        {
            if (!Game.IsDebugMode) 
                return;
            
            Game.Console.Draw(0, 0, $"Pos  : {Rover.Position}");
            Game.Console.Draw(0, 1, $"SPos : {Rover.GetScreenPos()}");
            Game.Console.Draw(0, 2, $"BB   : {Rover.BoundingBox}");
        }
    }
}