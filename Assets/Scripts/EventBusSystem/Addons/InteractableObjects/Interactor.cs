using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Interactor : MonoBehaviour
{
    [Header("Runtime Debug")]
    [SerializeField] private bool onInteractArea;
    [SerializeField] private GameObject interactObject;
    [SerializeField] private Interactable interactableScript;

    private readonly List<Interactable> _candidates = new();

    public void Interact()
    {
        if (!onInteractArea) return;
        interactableScript.Use();
    }

    private void OnTriggerEnter(Collider other)
    {
        var interactable = other.GetComponent<Interactable>();
        if (interactable != null && !_candidates.Contains(interactable))
            _candidates.Add(interactable);
    }

    private void OnTriggerExit(Collider other)
    {
        var interactable = other.GetComponent<Interactable>();
        if (interactable != null)
            _candidates.Remove(interactable);
    }

    private void Update()
    {
        CleanCandidates();
        PickClosest();
    }

    private void CleanCandidates()
    {
        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            var c = _candidates[i];
            if (c == null || !c.gameObject.activeInHierarchy)
                _candidates.RemoveAt(i);
        }
    }

    private void PickClosest()
    {
        Interactable closest = null;
        float bestDist = float.MaxValue;

        foreach (var candidate in _candidates)
        {
            float dist = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = candidate;
            }
        }

        interactableScript = closest;
        interactObject = closest != null ? closest.gameObject : null;
        onInteractArea = closest != null;
    }
}