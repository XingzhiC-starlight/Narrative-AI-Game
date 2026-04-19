using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

public class ChatUIController : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private GameObject chatUIRoot;
    [SerializeField] private Button continueButton;
    [SerializeField] private CanvasGroup chatCanvasGroup;
    [SerializeField] private float fadeDuration = 0.25f;

    private bool commandRegistered;
    private YarnTaskCompletionSource waitForContinueSource;
    private bool isWaitingForContinue;
    private bool isTransitioning;

    private void Awake()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (chatUIRoot != null && chatCanvasGroup == null)
        {
            chatCanvasGroup = chatUIRoot.GetComponent<CanvasGroup>();
            if (chatCanvasGroup == null)
            {
                chatCanvasGroup = chatUIRoot.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnEnable()
    {
        RegisterCommand();

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnClickContinue);
        }
    }

    private void OnDisable()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnClickContinue);
        }

        UnregisterCommand();
        CancelPendingWait();
    }

    private void RegisterCommand()
    {
        if (commandRegistered)
        {
            return;
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            Debug.LogWarning("ChatUIController requires a DialogueRunner reference.");
            return;
        }

        dialogueRunner.AddCommandHandler("chat_wait", (Func<YarnTask>)WaitForContinueAsync);
        commandRegistered = true;
    }

    private void UnregisterCommand()
    {
        if (!commandRegistered || dialogueRunner == null)
        {
            return;
        }

        dialogueRunner.RemoveCommandHandler("chat_wait");
        commandRegistered = false;
    }

    private async YarnTask WaitForContinueAsync()
    {
        if (chatUIRoot == null)
        {
            Debug.LogWarning("ChatUIController missing chatUIRoot. <<chat_wait>> will continue immediately.");
            return;
        }

        if (continueButton == null)
        {
            Debug.LogWarning("ChatUIController missing continueButton. <<chat_wait>> will continue immediately.");
            return;
        }

        if (isWaitingForContinue && waitForContinueSource != null)
        {
            await waitForContinueSource.Task;
            return;
        }

        isWaitingForContinue = true;
        waitForContinueSource = new YarnTaskCompletionSource();
        isTransitioning = true;

        await ShowChatUIAsync();
        continueButton.interactable = true;
        continueButton.Select();
        isTransitioning = false;

        await waitForContinueSource.Task;
    }

    public void OnClickContinue()
    {
        Debug.Log("[ChatUIController] Continue button clicked.");

        if (!isWaitingForContinue)
        {
            return;
        }

        if (isTransitioning)
        {
            return;
        }

        CompleteWaitAsync();
    }

    private async void CompleteWaitAsync()
    {
        isTransitioning = true;
        continueButton.interactable = false;
        await HideChatUIAsync();
        isWaitingForContinue = false;
        waitForContinueSource?.TrySetResult();
        waitForContinueSource = null;
        isTransitioning = false;
    }

    private void CancelPendingWait()
    {
        if (!isWaitingForContinue)
        {
            return;
        }

        chatUIRoot?.SetActive(false);
        isWaitingForContinue = false;
        waitForContinueSource?.TrySetCanceled();
        waitForContinueSource = null;
        isTransitioning = false;
    }

    private async YarnTask ShowChatUIAsync()
    {
        chatUIRoot.SetActive(true);

        if (chatCanvasGroup == null || fadeDuration <= 0f)
        {
            return;
        }

        chatCanvasGroup.alpha = 0f;
        await Effects.FadeAlphaAsync(chatCanvasGroup, 0f, 1f, fadeDuration, CancellationToken.None);
    }

    private async YarnTask HideChatUIAsync()
    {
        if (chatUIRoot == null || !chatUIRoot.activeSelf)
        {
            return;
        }

        if (chatCanvasGroup != null && fadeDuration > 0f)
        {
            await Effects.FadeAlphaAsync(chatCanvasGroup, chatCanvasGroup.alpha, 0f, fadeDuration, CancellationToken.None);
        }

        chatUIRoot.SetActive(false);
    }
}
