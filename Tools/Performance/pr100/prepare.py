#!/usr/bin/env python3
"""Materialize pinned, isolated benchmark subjects in a disposable worktree."""
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
MAIN = "7c81c920b1c16678748a090c69b877676bb31ab1"
PR = "be39689cc83f9ce694a01356314c4485ed1023d5"
EDITOR = "Assets/MackySoft/MackySoft.SerializeReferenceExtensions/Editor"
DEST = ROOT / "Assets/MackySoft/MackySoft.SerializeReferenceExtensions/Tests/Editor/Pr100Generated"
NS = "MackySoft.SerializeReferenceExtensions.Performance"


def source(revision, path):
    return subprocess.check_output(["git", "show", f"{revision}:{path}"], cwd=ROOT).decode("utf-8-sig")


def instrument(text, caller):
    # The probe is a separate copy: uninstrumented timings never run these wrappers.
    prefix = f'global::{NS}.ApiProbe'
    replacements = {
        "AppDomain.CurrentDomain.GetAssemblies()": f'{prefix}.Assemblies("{caller}")',
        "asm.GetTypes()": f'{prefix}.Types(asm, "{caller}")',
        "TypeCache.GetTypesDerivedFrom(baseType)": f'{prefix}.Derived(baseType, "{caller}")',
        "Attribute.GetCustomAttribute(type, typeof(AddTypeMenuAttribute))": f'{prefix}.Attribute(type, "{caller}")',
        "ObjectNames.NicifyVariableName(": f'{prefix}.Nicify(',
    }
    for old, new in replacements.items():
        text = text.replace(old, new)
    text = re.sub(r'Attribute.IsDefined\((\w+), typeof\((\w+)\)\)',
                  lambda m: f'{prefix}.IsDefined({m[1]}, typeof({m[2]}), "{caller}")', text)
    text = re.sub(r'(\w+)\.Get(GenericArguments|GenericTypeDefinition|Interfaces)\(\)',
                  lambda m: f'{prefix}.{m[2]}({m[1]}, "{caller}")', text)
    text = re.sub(r'(\w+)\.ContainsGenericParameters\b', lambda m: f'{prefix}.ContainsGeneric({m[1]}, "{caller}")', text)
    text = re.sub(r'(\w+)\.IsGenericType\b', lambda m: f'{prefix}.IsGeneric({m[1]}, "{caller}")', text)
    text = re.sub(r'(\w+)\.IsAssignableFrom\((\w+)\)',
                  lambda m: f'{prefix}.Assignable({m[1]}, {m[2]}, "{caller}")', text)
    text = text.replace('typeof(UnityEngine.Object).IsAssignableFrom(candiateType)',
                        f'{prefix}.Assignable(typeof(UnityEngine.Object), candiateType, "{caller}")')
    text = text.replace('openType.MakeGenericType(typeArgs)',
                        f'{prefix}.MakeGeneric(openType, typeArgs, "{caller}")')
    return text


def cache_method(text, signature, cache, key, value_type):
    # Scope the original return statements so each cache owns its complete result.
    start = text.index(signature)
    opening = text.index('{', start)
    depth = 1
    closing = opening + 1
    while depth:
        if text[closing] == '{':
            depth += 1
        elif text[closing] == '}':
            depth -= 1
        closing += 1
    body = text[opening + 1:closing - 1]
    body = re.sub(r'return ([^;]+);', lambda m: f'return {cache}[{key}] = {m[1]};', body)
    intro = f'\n            if ({cache}.TryGetValue({key}, out {value_type} cached)) {{ return cached; }}\n'
    return text[:opening + 1] + intro + body + text[closing - 1:]


def generate():
    DEST.mkdir(parents=True, exist_ok=True)
    search_paths = subprocess.check_output(["git", "ls-tree", "-r", "--name-only", MAIN, f"{EDITOR}/TypeSearch"], cwd=ROOT).decode().splitlines()
    for name, revision, probed in [("Baseline", MAIN, False), ("PolicyOnly", MAIN, False), ("Proposal", PR, False),
                                   ("ProposalNoGeneration", PR, False),
                                   ("ProbeBaseline", MAIN, True), ("ProbePolicyOnly", MAIN, True), ("ProbeProposal", PR, True),
                                   ("ProbeProposalNoGeneration", PR, True)]:
        paths = [f'{EDITOR}/AdvancedTypePopup.cs', f'{EDITOR}/TypeMenuUtility.cs'] + [p for p in search_paths if p.endswith('.cs')]
        for path in paths:
            selected_revision = PR if 'PolicyOnly' in name and path.endswith('DefaultIntrinsicTypePolicy.cs') else revision
            code = source(selected_revision, path).replace('namespace MackySoft.SerializeReferenceExtensions.Editor', f'namespace {NS}.{name}')
            if 'NoGeneration' in name and path.endswith('Unity_2023_2_OrNewer_TypeCandiateProvider.cs'):
                code = re.sub(r'                // New: try to close open generic candidates.*?\n                }\n', '', code, count=1, flags=re.S)
            if probed:
                code = instrument(code, Path(path).stem)
            (DEST / f'{name}_{Path(path).name}').write_text(code)
        (DEST / f'{name}_Popup.cs').write_text(f'''using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
namespace {NS}.{name} {{
    public sealed class BenchPopup : AdvancedTypePopup, global::MackySoft.SerializeReferenceExtensions.Performance.Pr100PerformanceInvestigation.IPopup {{
        public BenchPopup(IEnumerable<Type> types) : base(types, 13, new AdvancedDropdownState()) {{ }}
        public AdvancedDropdownItem Build() {{ return BuildRoot(); }}
        public static global::MackySoft.SerializeReferenceExtensions.Performance.Pr100PerformanceInvestigation.IPopup Create(Type[] types) {{ return new BenchPopup(types); }}
    }}
}}
''')

    for name in ['AttributeOnly', 'PathOnly', 'NicifyOnly', 'PresortOnly']:
        menu = source(MAIN, f'{EDITOR}/TypeMenuUtility.cs')
        popup = source(MAIN, f'{EDITOR}/AdvancedTypePopup.cs')
        if name == 'AttributeOnly':
            menu = menu.replace('public const string NullDisplayName', 'private static readonly Dictionary<Type, AddTypeMenuAttribute> cache = new Dictionary<Type, AddTypeMenuAttribute>();\n        public const string NullDisplayName')
            menu = cache_method(menu, 'public static AddTypeMenuAttribute GetAttribute', 'cache', 'type', 'AddTypeMenuAttribute')
        elif name == 'PathOnly':
            menu = menu.replace('public const string NullDisplayName', 'private static readonly Dictionary<Type, string[]> cache = new Dictionary<Type, string[]>();\n        public const string NullDisplayName')
            menu = cache_method(menu, 'public static string[] GetSplittedTypePath', 'cache', 'type', 'string[]')
        elif name == 'NicifyOnly':
            menu = menu.replace('public const string NullDisplayName', '''private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        public static string CachedNicifyVariableName(string name) {
            if (cache.TryGetValue(name, out string cached)) { return cached; }
            return cache[name] = ObjectNames.NicifyVariableName(name);
        }
        public const string NullDisplayName''')
            popup = popup.replace('ObjectNames.NicifyVariableName(', 'TypeMenuUtility.CachedNicifyVariableName(')
        else:
            # Exact PR sort movement, including its changed public AddTo behavior.
            popup = popup.replace('Type[] typeArray = types.OrderByType().ToArray();', 'Type[] typeArray = types as Type[] ?? types.ToArray();')
            popup = popup.replace('this.types = types.ToArray();', 'this.types = types.OrderByType().ToArray();')
        for filename, code in [('TypeMenuUtility.cs', menu), ('AdvancedTypePopup.cs', popup)]:
            (DEST / f'{name}_{filename}').write_text(code.replace('namespace MackySoft.SerializeReferenceExtensions.Editor', f'namespace {NS}.{name}'))
        template = (DEST / 'Baseline_Popup.cs').read_text()
        (DEST / f'{name}_Popup.cs').write_text(template.replace(f'{NS}.Baseline', f'{NS}.{name}'))

    for name in ['ApiProbe.cs', 'Pr100PerformanceInvestigation.cs']:
        (DEST / name).write_text((Path(__file__).parent / name).read_text())
    (DEST / 'subjects.json').write_text(json.dumps({'main': MAIN, 'pr': PR}, indent=2))
    print(DEST.relative_to(ROOT))


if __name__ == '__main__':
    generate()
