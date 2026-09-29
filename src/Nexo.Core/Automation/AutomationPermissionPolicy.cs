namespace Nexo.Core.Automation;

public static class AutomationPermissionPolicy
{
    public static AutomationRiskLevel GetRisk(AutomationAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Los argumentos forman parte de la evaluación tipada de riesgo: abrir una aplicación
        // y ejecutar un comando arbitrario a través de ella no son la misma acción, aunque
        // compartan el tipo. Ver `ShellExecutionPolicy` y el defecto D2 de la fase 1.1.
        if (action.Type == AutomationActionType.OpenApplication &&
            ShellExecutionPolicy.RequiresConfirmationToOpen(action.Target, action.Arguments))
        {
            return AutomationRiskLevel.Sensitive;
        }

        return action.Type switch
        {
            AutomationActionType.OpenTerminal => AutomationRiskLevel.Sensitive,
            AutomationActionType.OpenApplication or
            AutomationActionType.OpenFolder or
            AutomationActionType.SetApplicationVolume or
            AutomationActionType.MuteApplication or
            AutomationActionType.UnmuteApplication or
            AutomationActionType.StartFocus or
            AutomationActionType.StartBreak => AutomationRiskLevel.Reversible,
            AutomationActionType.CreateTask => AutomationRiskLevel.Safe,
            _ => AutomationRiskLevel.Blocked
        };
    }

    public static bool RequiresConfirmation(RoutineDefinition routine) =>
        routine.RequiresConfirmation ||
        routine.Steps.Any(step =>
            step.IsEnabled && GetRisk(step) == AutomationRiskLevel.Sensitive);

    public static bool IsAllowed(AutomationAction action, out string error)
    {
        error = string.Empty;

        if (!action.IsEnabled)
        {
            return true;
        }

        switch (action.Type)
        {
            case AutomationActionType.OpenApplication:
                if (string.IsNullOrWhiteSpace(action.Target))
                {
                    error = "La acción necesita una aplicación.";
                    return false;
                }
                return true;

            case AutomationActionType.OpenFolder:
            case AutomationActionType.OpenTerminal:
                if (string.IsNullOrWhiteSpace(action.WorkingDirectory))
                {
                    error = "La acción necesita una carpeta.";
                    return false;
                }
                return true;

            case AutomationActionType.SetApplicationVolume:
                if (string.IsNullOrWhiteSpace(action.Target) || !action.NumericValue.HasValue)
                {
                    error = "La acción de volumen necesita aplicación y porcentaje.";
                    return false;
                }
                if (action.NumericValue is < 0 or > 100)
                {
                    error = "El porcentaje debe estar entre 0 y 100.";
                    return false;
                }
                return true;

            case AutomationActionType.MuteApplication:
            case AutomationActionType.UnmuteApplication:
                if (string.IsNullOrWhiteSpace(action.Target))
                {
                    error = "La acción de audio necesita una aplicación.";
                    return false;
                }
                return true;

            case AutomationActionType.StartFocus:
            case AutomationActionType.StartBreak:
                if (!action.NumericValue.HasValue || action.NumericValue is < 1 or > 1440)
                {
                    error = "La sesión debe durar entre 1 y 1440 minutos.";
                    return false;
                }
                return true;

            case AutomationActionType.CreateTask:
                if (string.IsNullOrWhiteSpace(action.Text))
                {
                    error = "La acción necesita el título de la tarea.";
                    return false;
                }
                return true;

            default:
                error = "La acción no está permitida por Sakura.";
                return false;
        }
    }
}
