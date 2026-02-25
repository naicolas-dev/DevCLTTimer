using System.Windows.Input;

namespace DevCLT.WindowsApp.ViewModels;

public class OnboardingViewModel : ViewModelBase
{
    private readonly OnboardingStep[] _steps =
    {
        new(
            "Bem-vindo ao Dev CLT Timer",
            "Configure sua jornada e acompanhe trabalho, pausa e hora extra em um fluxo simples para o dia a dia."
        ),
        new(
            "Controle rapido com hotkeys e bandeja",
            "Use atalhos globais para iniciar jornada, pausa e hora extra mesmo com o app minimizado na bandeja do sistema."
        ),
        new(
            "Dados salvos localmente",
            "O app salva seu progresso em SQLite, permite recuperar sessao ativa e consultar historico com exportacao CSV."
        )
    };

    private int _currentStepIndex;

    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            if (SetField(ref _currentStepIndex, value))
                NotifyStepChanged();
        }
    }

    public int TotalSteps => _steps.Length;
    public string StepNumberText => $"{CurrentStepIndex + 1}/{TotalSteps}";
    public string Title => _steps[CurrentStepIndex].Title;
    public string Description => _steps[CurrentStepIndex].Description;
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => CurrentStepIndex == TotalSteps - 1;
    public string PrimaryButtonText => IsLastStep ? "Concluir" : "Proximo";

    public ICommand NextCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand SkipCommand { get; }

    public event Action? Finished;

    public OnboardingViewModel()
    {
        NextCommand = new RelayCommand(GoNext);
        BackCommand = new RelayCommand(GoBack, () => !IsFirstStep);
        SkipCommand = new RelayCommand(Complete);
    }

    public void Reset()
    {
        if (CurrentStepIndex == 0)
        {
            NotifyStepChanged();
            return;
        }

        CurrentStepIndex = 0;
    }

    private void GoNext()
    {
        if (IsLastStep)
        {
            Complete();
            return;
        }

        CurrentStepIndex++;
    }

    private void GoBack()
    {
        if (IsFirstStep) return;
        CurrentStepIndex--;
    }

    private void Complete()
    {
        Finished?.Invoke();
    }

    private void NotifyStepChanged()
    {
        OnPropertyChanged(nameof(StepNumberText));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(PrimaryButtonText));
        CommandManager.InvalidateRequerySuggested();
    }

    private readonly record struct OnboardingStep(string Title, string Description);
}
