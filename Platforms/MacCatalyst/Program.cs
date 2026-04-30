using ObjCRuntime;
using UIKit;

namespace SigmaChess
{
    // Стандартная точка входа Mac Catalyst-приложения (зеркало iOS/Program.cs).
    // Шаблон MAUI создаёт её автоматически — мы здесь ничего не меняем.
    public class Program
    {
        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }
}
