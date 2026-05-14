using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SigmaChess.ViewModels;

/// <summary>
/// База для всех ViewModel-ей. Реализует <see cref="INotifyPropertyChanged"/>,
/// чтобы XAML-биндинги обновлялись при изменении свойств.
/// </summary>
/// <remarks>
/// Свойство <see cref="IsBusy"/> оформлено как в примерах eldan (LoginBaseApp):
/// публичный get/set, в setter — сравнение с полем и <c>OnPropertyChanged()</c> без имени.
/// </remarks>
public class ViewModelBase : INotifyPropertyChanged
{
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Индикатор длительной операции (как в учебных MVVM-примерах).</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
        }
    }

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
