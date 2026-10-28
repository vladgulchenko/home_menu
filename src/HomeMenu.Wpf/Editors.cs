using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using HomeMenu.Core;

namespace HomeMenu.Wpf;

public record Choice(int Id, string Name) { public override string ToString() => Name; }
public sealed class FormField : ObservableObject
{
    private string text = "";
    private Choice? selected;
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Text { get => text; set => Set(ref text, value); }
    public List<Choice>? Choices { get; init; }
    public Choice? Selected { get => selected; set => Set(ref selected, value); }
    public bool IsChoice => Choices != null;
    public bool IsMultiline { get; init; }
}
public sealed class IngredientRow : ObservableObject
{
    private Product? product;
    private string grams = "100";
    public Product? Product { get => product; set => Set(ref product, value); }
    public string Grams { get => grams; set => Set(ref grams, value); }
}
public interface IDialogService
{
    bool Confirm(string text);
    Task<bool> EditAsync(EditorViewModel editor);
    void Error(string message);
}
public sealed class DialogService(Window owner) : IDialogService
{
    public bool Confirm(string text) => MessageBox.Show(owner, text, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    public Task<bool> EditAsync(EditorViewModel editor)
    {
        var window = new EditorWindow { Owner = owner, DataContext = editor }; editor.RequestClose = () => window.DialogResult = true;
        return Task.FromResult(window.ShowDialog() == true);
    }
    public void Error(string message) => MessageBox.Show(owner, message, "HomeMenu", MessageBoxButton.OK, MessageBoxImage.Warning);
}
public sealed class EditorViewModel : ObservableObject
{
    private readonly Func<EditorViewModel, Task> save;
    private string baseline = "";
    private string error = "";
    public string Title { get; }
    public ObservableCollection<FormField> Fields { get; } = [];
    public ObservableCollection<IngredientRow> Ingredients { get; } = [];
    public List<Product> Products { get; set; } = [];
    public bool HasIngredients { get; set; }
    public IngredientRow? SelectedIngredient { get; set; }
    public string Error { get => error; private set => Set(ref error, value); }
    public bool Saved { get; private set; }
    public bool IsSaving => Save.IsRunning;
    public Action? RequestClose { get; set; }
    public AsyncCommand Save { get; }
    public RelayCommand AddIngredient { get; }
    public RelayCommand RemoveIngredient { get; }
    public EditorViewModel(string title, Func<EditorViewModel, Task> save)
    {
        Title = title; this.save = save; Save = new(SaveAsync);
        AddIngredient = new(_ => Ingredients.Add(new IngredientRow { Product = Products.FirstOrDefault() }));
        RemoveIngredient = new(_ => { if (SelectedIngredient != null) Ingredients.Remove(SelectedIngredient); });
    }
    public EditorViewModel Text(string key, string label, object? value = null, bool multiline = false)
    {
        Fields.Add(new FormField { Key = key, Label = label, Text = Convert.ToString(value, CultureInfo.GetCultureInfo("ru-RU")) ?? "", IsMultiline = multiline }); return this;
    }
    public EditorViewModel Select(string key, string label, List<Choice> choices, int? selected = null)
    {
        Fields.Add(new FormField { Key = key, Label = label, Choices = choices, Selected = choices.FirstOrDefault(c => c.Id == selected) ?? choices.FirstOrDefault() }); return this;
    }
    public string Value(string key) => Fields.Single(f => f.Key == key).Text;
    public decimal Number(string key) => Rules.Parse(Value(key), Fields.Single(f => f.Key == key).Label);
    public int Integer(string key)
    {
        var n = Number(key); if (n != decimal.Truncate(n) || n < int.MinValue || n > int.MaxValue) throw new BusinessException("Введите целое число."); return (int)n;
    }
    public int Selected(string key) => Fields.Single(f => f.Key == key).Selected?.Id ?? throw new BusinessException("Выберите значение.");
    public DateOnly? Date(string key)
    {
        var text = Value(key).Trim(); if (text.Length == 0) return null;
        if (!DateOnly.TryParseExact(text, ["dd.MM.yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new BusinessException("Дата: дд.мм.гггг или пустое поле."); return date;
    }
    private string Fingerprint() => string.Join("\u001f", Fields.Select(f => $"{f.Key}={f.Text}/{f.Selected?.Id}"))
        + string.Join("\u001f", Ingredients.Select(i => $"{i.Product?.Id}={i.Grams}"));
    public void MarkClean() => baseline = Fingerprint();
    public bool IsDirty => baseline != Fingerprint();
    private async Task SaveAsync()
    {
        try { Error = ""; await save(this); Saved = true; RequestClose?.Invoke(); }
        catch (BusinessException ex) { Error = ex.Message; }
        catch (Exception ex) { App.Log(ex); Error = "Не удалось сохранить данные. Проверьте доступ к файлу базы данных."; }
    }
}
