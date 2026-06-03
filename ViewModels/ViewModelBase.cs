using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SigmaChess.ViewModels;

// Базовый класс для всех ViewModel (моделей представления). Даёт две общие вещи:
// уведомление интерфейса об изменении свойств (INotifyPropertyChanged) и флаг IsBusy
// (идёт ли загрузка). От него наследуются почти все остальные ViewModel, чтобы не
// повторять этот код. В шаблоне MVVM ViewModel — это «прослойка» между данными и
// экраном (страницей .xaml).
public class ViewModelBase : INotifyPropertyChanged
{
    private bool _isBusy;

    // Событие, на которое подписан интерфейс: «такое-то свойство изменилось,
    // перерисуй». Срабатывает из OnPropertyChanged.
    public event PropertyChangedEventHandler? PropertyChanged;

    // Признак «идёт работа/загрузка». Удобно показывать индикатор и блокировать
    // кнопки. При изменении уведомляет интерфейс.
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

    // Сообщает интерфейсу, что свойство изменилось (и его надо перечитать/перерисовать).
    // Имя свойства подставляется автоматически благодаря [CallerMemberName], поэтому из
    // сеттера можно звать просто OnPropertyChanged() без аргументов.
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
