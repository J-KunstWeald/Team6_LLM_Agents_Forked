using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;

namespace com.csutil.model.immutable {

    public static class ImmutableExtensions {

        public static Action AddStateChangeListenerDebounced<T, S>(this IDataStore<T> self, Func<T, S> getSubState, Action<S> onChanged, double delayInMs, bool triggerInstantToInit = true) {
            return self.AddStateChangeListener(getSubState, onChanged.AsThrottledDebounce(delayInMs), triggerInstantToInit);
        }

        public static Action AddAsyncStateChangeListener<T, S>(this IDataStore<T> s, Func<T, S> getSubState, Func<S, Task> onChanged) {
            return AddStateChangeListener(s, getSubState, (subState) => {
                onChanged(subState).LogOnError();
            });
        }

        public static Action AddStateChangeListener<T, S>(this IDataStore<T> self, Func<T, S> getSubState, Action<S> onChanged, bool triggerInstantToInit = true) {
            Action newListener = NewSubstateChangeListener(() => getSubState(self.GetState()), onChanged);
            self.onStateChanged += newListener;
            if (triggerInstantToInit) { onChanged(getSubState(self.GetState())); }
            return newListener;
        }

        public static Action AddStateChangeListener<T, S>(this IDataStore<T> self, S mutableObj, Action<S> onChanged, bool triggerInstantToInit = true) where S : IsMutable {
            Action newListener = () => {
                if (StateCompare.WasModifiedInLastDispatch(mutableObj)) {
                    onChanged(mutableObj);
                }
            };
            self.onStateChanged += newListener;
            if (triggerInstantToInit) { onChanged(mutableObj); }
            return newListener;
        }

        public static SubListeners<S> NewSubStateListener<T, S>(this IDataStore<T> self, Func<T, S> getSubState) {
            var subListener = new SubListeners<S>(getSubState(self.GetState()));
            var ownListenerInParent = self.AddStateChangeListener(getSubState, newSubState => { subListener.OnSubstateChanged(newSubState); });
            subListener.SetUnregisterInParentAction(() => { self.onStateChanged -= ownListenerInParent; });
            return subListener;
        }

        public static SubListeners<S> NewSubStateListener<T, S>(this SubListeners<T> self, Func<T, S> getSubState) {
            var subListener = new SubListeners<S>(getSubState(self.latestSubState));
            var ownListenerInParent = self.AddStateChangeListener(getSubState, newSubState => { subListener.OnSubstateChanged(newSubState); });
            subListener.SetUnregisterInParentAction(() => { self.innerListeners -= ownListenerInParent; });
            return subListener;
        }

        internal static Action NewSubstateChangeListener<S>(Func<S> getSubstate, Action<S> onChanged) {
            var oldState = getSubstate();
            var oldMonitor = GetMonitorFor(oldState);
            Action newListener = () => {
                var newState = getSubstate();
                bool stateChanged = StateCompare.WasModified(oldState, newState);
                if (stateChanged || StateCompare.WasModified(oldMonitor, GetMonitorFor(newState))) {
                    onChanged(newState);
                    oldState = newState;
                    oldMonitor = GetMonitorFor(newState);
                }
            };
            return newListener;
        }

        private static object GetMonitorFor(object state) {
            if (state is ICollection c) { return c.Count; }
            return null;
        }

        public static ImmutableList<T> MutateEntries<T>(this ImmutableList<T> list, object action, StateReducer<T> reducer) {
            if (list != null) {
                foreach (var elem in list) {
                    var newElem = reducer(elem, action);
                    if (StateCompare.WasModified(elem, newElem)) {
                        list = list.Replace(elem, newElem);
                    }
                }
            }
            return list;
        }

        [Obsolete("Typically not needed, since the key should be used to modify the specific entries of the Dict instead of iterating over all")]
        public static ImmutableDictionary<T, V> MutateEntries<T, V>(this ImmutableDictionary<T, V> dict, object action, StateReducer<V> reducer) {
            if (dict != null) {
                foreach (var elem in dict) {
                    var newValue = reducer(elem.Value, action);
                    if (StateCompare.WasModified(elem.Value, newValue)) {
                        dict = dict.SetItem(elem.Key, newValue);
                    }
                }
            }
            return dict;
        }

        public static ImmutableDictionary<T, V> MutateEntry<T, V>(this ImmutableDictionary<T, V> dict, T key, object action, StateReducer<V> reducer) {
            var elem = dict[key];
            var newValue = reducer(elem, action);
            if (StateCompare.WasModified(elem, newValue)) { dict = dict.SetItem(key, newValue); }
            return dict;
        }

        public static IList<T> MutateEntries<T>(this IList<T> list, object action, StateReducer<T> reducer, ref bool changed) {
            if (list != null) {
                for (int i = 0; i < list.Count; i++) {
                    var elem = list[i];
                    var newElem = reducer(elem, action);
                    if (StateCompare.WasModified(elem, newElem)) {
                        list[i] = newElem;
                        changed = true;
                    }
                }
            }
            return list;
        }

        public static ImmutableList<T> AddOrCreate<T>(this ImmutableList<T> self, T t) { return (self == null) ? ImmutableList.Create(t) : self.Add(t); }

        public static ImmutableDictionary<K, T> AddOrCreate<K, T>(this ImmutableDictionary<K, T> self, K key, T t) {
            if (self == null) { self = ImmutableDictionary<K, T>.Empty; }
            return self.Add(key, t);
        }

        public static T Mutate<T>(this T self, object action, StateReducer<T> reducer, ref bool changed) {
            return self.Mutate<T>(true, action, reducer, ref changed);
        }

        public static T Mutate<T>(this T self, bool applyReducer, object action, StateReducer<T> reducer, ref bool changed) {
            if (!applyReducer) { return self; }
            var newVal = reducer(self, action);
            changed = changed || StateCompare.WasModified(self, newVal);
            AssertValid(self, newVal);
            return newVal;
        }

        /// <summary> Helpfull when the parent object is needed to mutate a field</summary>
        public static T MutateField<P, T>(this P self, T field, object action, FieldReducer<P, T> reducer, ref bool changed) {
            return field.Mutate(action, (previousState, a) => { return reducer(self, previousState, a); }, ref changed);
        }

        [Conditional("DEBUG"), Conditional("ENFORCE_ASSERTIONS")] // Stripped from production code
        private static void AssertValid<T>(T oldVal, T newVal) {
            if (oldVal is IsValid v1) {
                AssertV2.IsTrue(v1.IsValid(), "Old value before mutation invalid");
            }
            if (StateCompare.WasModified(oldVal, newVal) && newVal is IsValid v2) {
                AssertV2.IsTrue(v2.IsValid(), "New value after mutation invalid");
            }
        }

        public static ForkedStore<T> NewFork<T>(this DataStore<T> s) {
            return new ForkedStore<T>(s, s.reducer);
        }

        /// <summary> Creates a selector to access parts of the state and stay updated automatically </summary>
        public static Func<V> SelectElement<T, V>(this IDataStore<T> self, Func<T, V> getElement) {
            V latestState = getElement(self.GetState());
            self.AddStateChangeListener(getElement, (newState) => { latestState = newState; });
            return () => { return latestState; };
        }

        /// <summary> Creates a selector that efficiently gets a list entry and stays updated automatically </summary>
        public static Func<V> SelectListEntry<T, V>(this IDataStore<T> self, Func<T, ImmutableList<V>> getList, Predicate<V> match) {
            ImmutableList<V> latestList = getList(self.GetState());
            V found = latestList.Find(match);
            int latestPos = latestList.IndexOf(found);
            self.AddStateChangeListener(getList, (changedList) => { latestList = changedList; });
            return () => {
                if (latestList.Count > latestPos) { // First check at the latest known position:
                    V eleAtLastPos = latestList[latestPos];
                    if (match(eleAtLastPos)) { return eleAtLastPos; }
                }
                found = latestList.Find(match);
                latestPos = latestList.IndexOf(found);
                return found;
            };
        }

    }

    /// <summary> Similar to the StateReducer but provides the parent context of the field as well </summary>
    /// /// <param name="action"> The action to be applied to the state tree. </param>
    /// /// <returns> The new field value. </returns>
    public delegate T FieldReducer<P, T>(P parent, T oldFieldValue, object action);

    /// <summary> ts a method that is used to update the state tree. </summary>
    /// <param name="action"> The action to be applied to the state tree. </param>
    /// <returns> The updated state tree. </returns>
    public delegate T StateReducer<T>(T previousState, object action);

    /// <summary> Represents a method that dispatches an action. </summary>
    /// <param name="action"> The action to dispatch. </param>
    public delegate object Dispatcher(object action);

    /// <summary> Represents a method that is used as middleware. </summary>
    /// <typeparam name="T">  The state tree type. </typeparam>
    /// <returns> A function that, when called with a <see cref="Dispatcher" />, returns a new <see cref="Dispatcher" /> that wraps the first one. </returns>
    public delegate Func<Dispatcher, Dispatcher> Middleware<T>(IDataStore<T> store);

    public class SubListeners<SubState> {

        public Action innerListeners;

        /// <summary> This is the action that can be called to unregister the <see cref="SubListeners{SubState}"/> in its parent again </summary>
        private Action unregisterInParentAction;

        public SubState latestSubState { get; private set; }
        public SubListeners(SubState currentSubState) { latestSubState = currentSubState; }

        public void OnSubstateChanged(SubState newSubState) {
            latestSubState = newSubState;
            innerListeners.InvokeIfNotNull();
        }

        public Action AddStateChangeListener<T>(Func<SubState, T> getSubSubState, Action<T> onChanged, bool triggerInstantToInit = true) {
            Action newListener = ImmutableExtensions.NewSubstateChangeListener(() => getSubSubState(latestSubState), onChanged);
            innerListeners += newListener;
            if (triggerInstantToInit) { onChanged(getSubSubState(latestSubState)); }
            return newListener;
        }

        public Action AddStateChangeListener<T, S>(S mutableObj, Action<S> onChanged, bool triggerInstantToInit = true) where S : IsMutable {
            Action newListener = () => {
                if (StateCompare.WasModifiedInLastDispatch(mutableObj)) { onChanged(mutableObj); }
            };
            innerListeners += newListener;
            if (triggerInstantToInit) { onChanged(mutableObj); }
            return newListener;
        }

        public void SetUnregisterInParentAction(Action unregisterInParentAction) {
            this.unregisterInParentAction = unregisterInParentAction;
        }

        /// <summary> Can be called to unregister the <see cref="SubListeners{SubState}"/> in its parent again. Afterwards it (and all its children) will no longer be informed about updates </summary>
        public void UnregisterFromParent() { unregisterInParentAction(); }

    }

}