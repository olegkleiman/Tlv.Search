using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SKDrive
{
    public class LightPlugin
    {
        public bool IsOn { get; set; } = false;
        
        [KernelFunction]
        [Description("Get the state of the light")]
        public string GetState() => this.IsOn ? "on" : "off";

        [KernelFunction]
        [Description("Changes the state of the light")]
        public string ChangeState(bool newState)
        {
            this.IsOn = newState;
            var state = this.GetState();

            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine($"[Light is now {state}]");
            Console.ResetColor();

            return state;
        }
    }
}
