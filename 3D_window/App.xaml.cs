using ReactiveUI.Builder;
using System.Windows;

namespace _3D_window
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Register the ReactiveUI core + WPF services (schedulers, message bus,
            // interaction interceptors, RxApp.MainThreadScheduler, ...).
            RxAppBuilder.CreateReactiveUIBuilder()
                .WithCoreServices()
                .WithWpf()
                .BuildApp();
        }
    }
}
