﻿using Microsoft.Extensions.DependencyInjection;

namespace WindowsFormsLifetime;

public interface IFormProvider
{
    /// <summary>
    /// Gets the requested form type and ensures it is created on the UI thread.
    /// </summary>
    /// <typeparam name="T">The form type to get.</typeparam>
    /// <returns>An instance of the form, asynchronously.</returns>
    Task<T> GetFormAsync<T>() where T : Form;

    /// <summary>
    /// Gets the requested form type and ensures it is created on the UI thread. Creates the form in the given scope.
    /// </summary>
    /// <typeparam name="T">The form type to get.</typeparam>
    /// <param name="scope">The scope in which the form should be created.</param>
    /// <returns>An instance of the form, asynchronously.</returns>
    Task<T> GetFormAsync<T>(IServiceScope scope) where T : Form;

    Task<Form> GetMainFormAsync();

    /// <summary>
    /// Gets the requested form type on the current thread. Should only be called on the UI thread. All scoped and transient dependencies will be disposed when the form is disposed.
    /// </summary>
    /// <typeparam name="T">The form type to get.</typeparam>
    /// <returns>An instance of the form.</returns>
    T GetForm<T>() where T : Form;

    /// <summary>
    /// Gets the requested form type on the current thread. Should only be called on the UI thread.  Creates the form in the given scope.
    /// </summary>
    /// <typeparam name="T">The form type to get.</typeparam>
    /// <param name="scope">The scope in which the form should be created.</param>
    /// <returns>An instance of the form.</returns>
    T GetForm<T>(IServiceScope scope) where T : Form;
}

public class FormProvider : IFormProvider
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly IServiceProvider _serviceProvider;
    private readonly IWindowsFormsSynchronizationContextProvider _syncContextManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public FormProvider(
        IServiceProvider serviceProvider,
        IWindowsFormsSynchronizationContextProvider syncContextManager,
        IServiceScopeFactory serviceScopeFactory)
    {
        _serviceProvider = serviceProvider;
        _syncContextManager = syncContextManager;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<T> GetFormAsync<T>()
        where T : Form
    {
        // We are throttling this because there is only one gui thread
        await _semaphore.WaitAsync();

        var form = await _syncContextManager.SynchronizationContext.InvokeAsync(GetForm<T>);

        _semaphore.Release();

        return form;
    }

    public async Task<T> GetFormAsync<T>(IServiceScope scope) where T : Form
    {
        // We are throttling this because there is only one gui thread
        await _semaphore.WaitAsync();

        var form = await _syncContextManager.SynchronizationContext.InvokeAsync(() => scope.ServiceProvider.GetService<T>());

        _semaphore.Release();

        return form;
    }

    public Task<Form> GetMainFormAsync()
    {
        var applicationContext = _serviceProvider.GetService<ApplicationContext>();
        return Task.FromResult(applicationContext.MainForm);
    }

    public T GetForm<T>() where T : Form
    {
        T form = null;
        var scope = _serviceScopeFactory.CreateScope();
        try
        {
            form = scope.ServiceProvider.GetService<T>();
            if (form == null)
            {
                scope.Dispose();
            }
            else
            {
                form.Disposed += (s, e) => scope.Dispose();
            }
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        return form;
    }

    public T GetForm<T>(IServiceScope scope) where T : Form
        => scope.ServiceProvider.GetService<T>();

    public void Dispose() => _semaphore?.Dispose();
}
