namespace Maui.Controls.Sample;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		this.Build();
	}

	void Build()
	{
		Button button = new Button()
		{
			Text = "Click me again!",
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			BackgroundColor = Colors.OrangeRed,
		};

		button.Clicked += (s, a) => this.Build();

		Border border = Activator.CreateInstance<Border>();
		border.Stroke = new SolidColorBrush(Colors.LightBlue);
		border.StrokeThickness = 4;
		border.BackgroundColor = Colors.DarkBlue;

		Layout layout = new Grid()
		{
			BackgroundColor = Colors.YellowGreen,
			Children = { border, button }
		};

		Content = layout;
	}
}