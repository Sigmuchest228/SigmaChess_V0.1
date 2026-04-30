using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SigmaChess.ViewModels;

/// <summary>
/// База для всех ViewModel-ей. Реализует <see cref="INotifyPropertyChanged"/>,
/// чтобы XAML-биндинги обновлялись при изменении свойств.
/// </summary>
public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Бросает событие изменения свойства. <see cref="CallerMemberNameAttribute"/> позволяет
    /// не передавать имя руками: компилятор сам подставит имя свойства, в котором вызвана функция.
    /// Это убирает строки-«магии» и опечатки в именах.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
