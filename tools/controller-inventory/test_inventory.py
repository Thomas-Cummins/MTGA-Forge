"""Regression checks for the inventory itself; no Forge build or client needed."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    'controller_inventory', Path(__file__).resolve().parents[1] / 'controller-inventory.py')
inventory = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inventory)


class InventoryTests(unittest.TestCase):
    def test_java_parser_preserves_overloads_and_nested_class_boundaries(self):
        source = '''
class Ai {
    public Ai() {}
    // public void fake() {} must not be treated as a method.
    public int chooseNumber(String title, int min, int max) { return min; }
    public int chooseNumber(String title, java.util.List<Integer> values) { return 1; }
    public <T extends Thing> java.util.List<T> chooseMany(
        java.util.Map<String, java.util.List<T>> options, String[] flags) { return null; }
    private void secret() {}
    public static void utility() {}
}
class Host {
    public void chooseNumber(String title, java.util.List<Integer> values) {}
    private final class Human extends Ai {
        @Override public int chooseNumber(String title, int min, int max) {
            return super.chooseNumber(title, min, max);
        }
        public <T extends Thing> List<T> chooseMany(Map<String, List<T>> options, String[] flags) { return null; }
    }
}
'''
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'Fixture.java'
            path.write_text(source)
            rows = inventory.extract('java', [path])
        ai = inventory.method_map(rows, 'Ai')
        human = inventory.method_map(rows, 'Host.Human')
        self.assertEqual(len(ai), 3)  # No constructor, comment, private, or static method.
        self.assertEqual(set(ai) - set(human), {'chooseNumber(String,List<Integer>)'})
        generic = 'chooseMany(Map<String,List<T>>,String[])'
        self.assertIn(generic, human)  # Qualified and imported spellings must match.
        self.assertEqual(human['chooseNumber(String,int,int)']['super_calls'], ['chooseNumber'])

    def test_review_becomes_stale_when_override_changes(self):
        method = {'class': 'PlayerControllerAi', 'key': 'choose()', 'source': 'AI choice'}
        override = {'source': 'Human choice'}
        reviews = {'controller:choose()': {
            'status': 'Partial override', 'note': 'Reviewed branch',
            'fingerprint': inventory.fingerprint(method, override),
        }}
        self.assertEqual(inventory.reviewed_status(method, override, reviews)[0], 'Partial override')
        self.assertEqual(inventory.reviewed_status(method, {'source': 'Changed choice'}, reviews)[0], 'Review stale')
        self.assertEqual(inventory.reviewed_status(method, None, reviews)[0], 'Review stale')

    def test_new_method_requires_review(self):
        method = {'class': 'PlayerControllerAi', 'key': 'newChoice()', 'source': 'new AI choice'}
        self.assertEqual(inventory.reviewed_status(method, None, {})[0], 'Needs path review')

    def test_ambiguous_type_normalization_is_rejected(self):
        duplicate = {'class': 'Ai', 'key': 'choose(Card)'}
        with self.assertRaises(ValueError):
            inventory.method_map([duplicate, duplicate], 'Ai')


if __name__ == '__main__':
    unittest.main()
