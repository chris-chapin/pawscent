using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Pawscent.App.ViewModels;
using Pawscent.Core.Profiles;
using Pawscent.Core.Receipts;
using Pawscent.Core.Reports;
using Pawscent.Core.Storage;

namespace Pawscent.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AppPaths>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<ReceiptFiles>();
        // In order of preference: Excel for spreadsheets, LibreOffice for everything else and when Excel is missing.
        services.AddSingleton<IOfficeApp, ExcelConverter>();
        services.AddSingleton<IOfficeApp, LibreOfficeConverter>();
        services.AddSingleton<IOfficeConverter, OfficeConverter>();
        services.AddSingleton<ExpenseReportGenerator>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }
}
