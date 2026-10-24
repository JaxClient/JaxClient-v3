using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Resources;

namespace JaxClient_v3;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private sealed class LocalizedValue
    {
        public string Source { get; set; }
        public string Rendered { get; set; }
    }

    private static readonly ConditionalWeakTable<
        DependencyObject,
        Dictionary<DependencyProperty, LocalizedValue>>
        LocalizedValues = new();

    private Dictionary<string, string> translations =
        new(StringComparer.Ordinal);

    static LocalizationManager()
    {
        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(HandleElementLoaded),
            true);
    }

    private LocalizationManager()
    {
    }

    public static LocalizationManager Instance { get; } = new();

    public string Language { get; private set; } = "en";

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler LanguageChanged;

    public string this[string source] => Translate(source);

    public void SetLanguage(string language)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();

       
        string normalized = language?.Trim().ToLowerInvariant();
        if (normalized is not ("en" or "de"))
            throw new ArgumentException(
                $"Unsupported language: {language}",
                nameof(language));

    var assembly = typeof(LocalizationManager).Assembly;
    
    string resourceName =
        $"JaxClient_v3.Languages.{normalized}.json";
    
    using Stream? stream =
        assembly.GetManifestResourceStream(resourceName);
    
    if (stream == null)
    {
        throw new FileNotFoundException(
            $"Embedded resource not found: {resourceName}");
    }
    
    using StreamReader reader = new StreamReader(stream);
    
    string json = reader.ReadToEnd();

        Dictionary<string, string> loaded =
            JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        if (loaded == null)
            throw new InvalidDataException(
                $"Language resource is invalid: {normalized}.json");

        translations = new Dictionary<string, string>(
            loaded,
            StringComparer.Ordinal);
        Language = normalized;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Translate(string source)
    {
        if (source == null)
            return null;

        if (translations.TryGetValue(source, out string translated))
            return translated;

        foreach (KeyValuePair<string, string> entry in translations
                     .Where(pair => pair.Key.Contains("{0}", StringComparison.Ordinal))
                     .OrderByDescending(pair => pair.Key.Length))
        {
            string[] parts = entry.Key.Split(
                "{0}",
                2,
                StringSplitOptions.None);
            if (source.StartsWith(parts[0], StringComparison.Ordinal) &&
                source.EndsWith(parts[1], StringComparison.Ordinal) &&
                source.Length >= parts[0].Length + parts[1].Length)
            {
                string argument = source.Substring(
                    parts[0].Length,
                    source.Length - parts[0].Length - parts[1].Length);
                return entry.Value.Replace(
                    "{0}",
                    argument,
                    StringComparison.Ordinal);
            }
        }

        return source;
    }

    public string Format(string template, params object[] arguments)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            Translate(template),
            arguments);
    }

    public void ApplyToVisualTree(DependencyObject root)
    {
        if (root == null)
            return;

        TranslateElement(root);
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
            ApplyToVisualTree(VisualTreeHelper.GetChild(root, index));
    }

    private static void HandleElementLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject element)
            Instance.TranslateElement(element);
    }

    private void TranslateElement(DependencyObject element)
    {
        if (element is Window)
            TranslateProperty(element, Window.TitleProperty);

        if (element is TextBlock)
            TranslateProperty(element, TextBlock.TextProperty);
        else if (element is AccessText)
            TranslateProperty(element, AccessText.TextProperty);
        else if (element is Run)
            TranslateProperty(element, Run.TextProperty);

        if (element is HeaderedContentControl)
            TranslateProperty(element, HeaderedContentControl.HeaderProperty);
        if (element is HeaderedItemsControl)
            TranslateProperty(element, HeaderedItemsControl.HeaderProperty);

        if (element is ContentControl &&
            element is not ComboBoxItem)
        {
            TranslateProperty(element, ContentControl.ContentProperty);
        }

        if (element is UIElement)
            TranslateProperty(element, FrameworkElement.ToolTipProperty);
    }

    private void TranslateProperty(
        DependencyObject element,
        DependencyProperty property)
    {
        if (BindingOperations.IsDataBound(element, property))
            return;

        if (element.GetValue(property) is not string current)
            return;

        Dictionary<DependencyProperty, LocalizedValue> values =
            LocalizedValues.GetValue(
            element,
            _ => new Dictionary<DependencyProperty, LocalizedValue>());
        if (!values.TryGetValue(property, out LocalizedValue state))
        {
            state = new LocalizedValue();
            values.Add(property, state);
        }
        if (state.Source == null ||
            !string.Equals(
                current,
                state.Rendered,
                StringComparison.Ordinal))
        {
            state.Source = current;
        }

        string translated = Translate(state.Source);
        state.Rendered = translated;
        if (!string.Equals(current, translated, StringComparison.Ordinal))
            element.SetCurrentValue(property, translated);
    }
}
