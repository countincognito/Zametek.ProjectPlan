"""Makes THIRD-PARTY-NOTICES.txt at the root of the repository.

Run it after a change of a package or of the version of the application (docs/BUILDING.md, "The notices of the other
parties"). It needs Python 3.8 or later, a build of the desktop application and of zpp, and the runtime packs of .NET 10 in
the cache of NuGet. A self-contained publish puts the runtime packs there (for example `make publish-cli`).

    dotnet build Zametek.ProjectPlan.slnf
    python pkg/notices/make-notices.py

The script reads:

- the packages that the two programs ship: each package with files for the runtime in the deps.json file of each program
  (in bin/<configuration>/net10.0 of its project). The image of the Java runtime of IKVM goes into the output through the
  build files of IKVM.Image, and not as a file for the runtime. Thus, the script takes IKVM.Image from the restore of zpp
  (obj/project.assets.json);
- the license, the copyright and the project of each package, from its .nuspec file in the cache of NuGet, and the license
  files and the notices that the package has;
- the license and the notices of the runtimes of .NET and of ASP.NET Core, from the newest runtime packs of .NET 10 for
  win-x64 in the cache of NuGet;
- the version of the application, from Directory.Build.props;
- the texts of Apache-2.0 and LGPL-2.1 in the folder licenses next to this script (from /usr/share/common-licenses of
  Ubuntu), the text of BSD-2-Clause from LICENSE, and the text of the OFL from the font of Zametek.Graphs.Avalonia.

Three texts are not from a file of a package, because no package gives them: the text of the MS-PL (Svg.Custom), which is
the published text and is in this script, and the copyright of MSAGL (AutomaticGraphLayout) and of the font Inter, which
are from their projects.
"""
import argparse
import hashlib
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
CACHE = os.environ.get('NUGET_PACKAGES') or os.path.join(os.path.expanduser('~'), '.nuget', 'packages')

# The programs, with the folder of their project and the name of their deps.json file.
PROGRAMS = [
    ('Zametek.ProjectPlan.Desktop', 'projectplandotnet'),
    ('Zametek.ProjectPlan.CommandLine', 'zpp'),
]

# The kinds of files in a deps.json that go into a program.
ASSET_KINDS = ('runtime', 'native', 'runtimeTargets', 'resources')

# Only the Debug build of the desktop application has the developer tools.
NOT_SHIPPED = {'AvaloniaUI.DiagnosticsSupport'}

# Packages with no license data. The license is the one of the project on GitHub.
KNOWN = {
    'AutomaticGraphLayout': ('MIT', 'Copyright (c) Microsoft Corporation', 'https://github.com/microsoft/automatic-graph-layout'),
    'AutomaticGraphLayout.Drawing': ('MIT', 'Copyright (c) Microsoft Corporation', 'https://github.com/microsoft/automatic-graph-layout'),
}

RULE = '=' * 100
LINE = '-' * 100

MS_PL = '''Microsoft Public License (Ms-PL)

This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
accept the license, do not use the software.

1. Definitions

The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S.
copyright law.

A "contribution" is the original software, or any additions or changes to the software.

A "contributor" is any person that distributes its contribution under this license.

"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights

(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution,
prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.

(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each
contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use,
sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the
contribution in the software.

3. Conditions and Limitations

(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.

(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your
patent license from such contributor to the software ends automatically.

(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices
that are present in the software.

(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a
complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object
code form, you may only do so under a license that complies with this license.

(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties,
guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change.
To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness
for a particular purpose and non-infringement.
'''


def fail(message):
    sys.exit(f'make-notices: {message}')


def read(path):
    """The text of a file with LF line ends, no space at the end of a line, and one LF at the end."""
    data = open(path, 'rb').read()
    for encoding in ('utf-8-sig', 'cp1252'):
        try:
            text = data.decode(encoding)
            break
        except UnicodeDecodeError:
            continue
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    return '\n'.join(x.rstrip() for x in text.split('\n')).strip('\n') + '\n'


def version_of_application():
    props = ET.parse(os.path.join(REPO, 'Directory.Build.props')).getroot()
    prefix = props.findtext('.//VersionPrefix', '').strip()
    suffix = props.findtext('.//VersionSuffix', '').strip()
    if not prefix:
        fail('Directory.Build.props has no VersionPrefix')
    return f'{prefix}-{suffix}' if suffix else prefix


def shipped_packages(configuration):
    """Each package with files for the runtime in the deps.json files of the two programs, as {id: {versions}}. The two
    programs can ship two versions of one package, because NuGet takes the lowest version that each program allows."""
    shipped = {}
    for project, program in PROGRAMS:
        path = os.path.join(REPO, 'src', project, 'bin', configuration, 'net10.0', f'{program}.deps.json')
        if not os.path.isfile(path):
            fail(f'{path} is not there. Build the two programs first (dotnet build Zametek.ProjectPlan.slnf).')
        deps = json.load(open(path, encoding='utf-8-sig'))
        targets = list(deps['targets'].values())
        for key, library in deps['libraries'].items():
            if library.get('type') != 'package':
                continue
            if any(kind in target.get(key, {}) for target in targets for kind in ASSET_KINDS):
                package, version = key.split('/', 1)
                shipped.setdefault(package, set()).add(version)

    # The image of the Java runtime of IKVM, from the restore of zpp.
    assets = json.load(open(os.path.join(REPO, 'src', 'Zametek.ProjectPlan.CommandLine', 'obj', 'project.assets.json'), encoding='utf-8-sig'))
    for key, library in assets['libraries'].items():
        package, version = key.split('/', 1)
        if library.get('type') == 'package' and (package == 'IKVM.Image' or package.startswith('IKVM.Image.runtime.')):
            shipped.setdefault(package, set()).add(version)

    for package in NOT_SHIPPED:
        shipped.pop(package, None)
    return shipped


def newest_runtime_pack(name):
    folder = os.path.join(CACHE, name)
    versions = [x for x in os.listdir(folder) if re.fullmatch(r'10\.0\.[0-9]+', x)] if os.path.isdir(folder) else []
    if not versions:
        fail(f'no runtime pack of .NET 10 in {folder}. A self-contained publish puts it there (make publish-cli).')
    return os.path.join(folder, max(versions, key=lambda x: int(x.split('.')[2])))


def main():
    parser = argparse.ArgumentParser(description='Makes THIRD-PARTY-NOTICES.txt at the root of the repository.')
    parser.add_argument('--configuration', default='Debug', help='The configuration of the build to read (default: Debug).')
    parser.add_argument('--output', default=os.path.join(REPO, 'THIRD-PARTY-NOTICES.txt'), help='The file to write.')
    arguments = parser.parse_args()

    version = version_of_application()
    shipped = shipped_packages(arguments.configuration)
    runtime = newest_runtime_pack('microsoft.netcore.app.runtime.win-x64')
    aspnet = newest_runtime_pack('microsoft.aspnetcore.app.runtime.win-x64')

    license_files = {}   # hash -> (title, text, [packages])
    notice_files = {}    # hash -> (text, [packages])
    entries = []

    pairs = [(package, version) for package in sorted(shipped, key=str.lower) for version in sorted(shipped[package])]

    for package, version_of_package in pairs:
        folder = os.path.join(CACHE, package.lower(), version_of_package)
        nuspecs = [x for x in os.listdir(folder) if x.endswith('.nuspec')] if os.path.isdir(folder) else []
        if not nuspecs:
            fail(f'{package} {version_of_package} is not in the cache of NuGet ({folder}). Restore the solution first.')
        root = ET.parse(os.path.join(folder, nuspecs[0])).getroot()
        namespace = {'n': root.tag.split('}')[0].strip('{')}
        metadata = root.find('n:metadata', namespace)
        element = metadata.find('n:license', namespace)
        kind = element.get('type') if element is not None else None
        value = (element.text or '').strip() if element is not None else ''
        copyright_ = (metadata.findtext('n:copyright', '', namespace) or '').strip()
        project = (metadata.findtext('n:projectUrl', '', namespace) or '').strip()

        if package in KNOWN:
            name, copyright_, project = KNOWN[package]
            shown = f'{name} (from the project; the package has no license data)'
        elif kind == 'expression':
            shown = value
        elif kind == 'file':
            text = read(os.path.join(folder, value))
            key = hashlib.sha256(text.encode()).hexdigest()
            title = license_files.get(key, (f'License file {len(license_files) + 1}', text, []))[0]
            license_files.setdefault(key, (title, text, []))[2].append(f'{package} {version_of_package}')
            shown = f'the license file of the package ({title}, below)'
        else:
            shown = 'not given by the package'

        for item in sorted(os.listdir(folder)):
            path = os.path.join(folder, item)
            lower = item.lower()
            if os.path.isfile(path) and ('notice' in lower or 'third-party' in lower) and lower.endswith(('.txt', '.md')):
                text = read(path)
                key = hashlib.sha256(text.encode()).hexdigest()
                notice_files.setdefault(key, (text, []))[1].append(f'{package} {version_of_package} ({item})')
            elif os.path.isdir(path) and lower == 'notices':
                for sub in sorted(os.listdir(path)):
                    if sub.lower().endswith('.txt'):
                        text = read(os.path.join(path, sub))
                        key = hashlib.sha256(text.encode()).hexdigest()
                        notice_files.setdefault(key, (text, []))[1].append(f'{package} {version_of_package} (Notices/{sub})')

        entries.append((package, version_of_package, shown, copyright_, project))

    bsd2 = read(os.path.join(REPO, 'LICENSE'))
    bsd2_body = bsd2[bsd2.index('Redistribution and use'):]
    mit = read(os.path.join(runtime, 'LICENSE.TXT'))
    mit_body = mit[mit.index('Permission is hereby granted'):]

    out = []
    w = out.append
    w('THIRD-PARTY NOTICES')
    w('')
    w(f'projectplan.net and zpp, version {version}')
    w('')
    w('The desktop application (projectplandotnet) and the command line tool (zpp) contain software of other parties. This file')
    w('lists each package that a release contains, with its license and its copyright. Then it gives the text of each license,')
    w('the license files of the packages, and the notices that the packages give for the software of other parties in them.')
    w('')
    w('Each release is a self-contained application, and so it also contains the runtime of .NET. The section ".NET" gives its')
    w('license and its notices.')
    w('')
    w('The source code of projectplan.net is at https://github.com/countincognito/Zametek.ProjectPlan, under the license in the')
    w('file LICENSE of the repository (BSD 2-Clause).')
    w('')
    w('The license of MPXJ.Net is the GNU Lesser General Public License (LGPL), version 2.1 or later. Its source code is at')
    w('https://github.com/joniles/mpxj. The license of the Java class library in the image of IKVM is the GNU General Public')
    w('License, version 2, with the Classpath exception. Its source code is at https://github.com/ikvmnet/ikvm and')
    w('https://github.com/openjdk/jdk.')
    w('')
    w(RULE)
    w('PACKAGES')
    w(RULE)
    w('')
    for package, version_of_package, shown, copyright_, project in entries:
        w(f'{package} {version_of_package}')
        w(f'  License: {shown}')
        if copyright_:
            w(f'  Copyright: {copyright_}')
        if project:
            w(f'  Project: {project}')
        w('')

    w(RULE)
    w('FONTS')
    w(RULE)
    w('')
    w('Inter, in the package Avalonia.Fonts.Inter')
    w('  License: SIL Open Font License 1.1 (below)')
    w('  Copyright: Copyright (c) 2016 The Inter Project Authors (https://github.com/rsms/inter)')
    w('')
    w('Cascadia Mono, in Zametek.Graphs.Avalonia')
    w('  License: SIL Open Font License 1.1 (below)')
    w('  Copyright: Copyright (c) 2019 - Present, Microsoft Corporation, with Reserved Font Name Cascadia Code.')
    w('')

    w(RULE)
    w('.NET')
    w(RULE)
    w('')
    w('The runtime of .NET 10, and the runtime of ASP.NET Core 10 in zpp. License: MIT. The license and the notices of the two')
    w('runtimes follow.')
    w('')
    w(LINE)
    w(mit.rstrip('\n'))
    w('')
    w('Notices of the runtime of .NET')
    w(LINE)
    w(read(os.path.join(runtime, 'THIRD-PARTY-NOTICES.TXT')).rstrip('\n'))
    w('')
    w('Notices of the runtime of ASP.NET Core')
    w(LINE)
    w(read(os.path.join(aspnet, 'THIRD-PARTY-NOTICES.TXT')).rstrip('\n'))
    w('')

    w(RULE)
    w('LICENSE TEXTS')
    w(RULE)
    w('')
    w('MIT')
    w(LINE)
    w('The license of the packages that give the license MIT, with the copyright of each package above.')
    w('')
    w(mit_body.rstrip('\n'))
    w('')
    w('BSD-2-Clause')
    w(LINE)
    w('The license of the packages that give the license BSD-2-Clause, with the copyright of each package above.')
    w('')
    w(bsd2_body.rstrip('\n'))
    w('')
    w('Apache-2.0')
    w(LINE)
    w(read(os.path.join(HERE, 'licenses', 'Apache-2.0.txt')).rstrip('\n'))
    w('')
    w('MS-PL')
    w(LINE)
    w(MS_PL.rstrip('\n'))
    w('')
    w('LGPL-2.1-or-later')
    w(LINE)
    w(read(os.path.join(HERE, 'licenses', 'LGPL-2.1.txt')).rstrip('\n'))
    w('')
    w('SIL Open Font License 1.1')
    w(LINE)
    w(read(os.path.join(REPO, 'src', 'Zametek.Graphs.Avalonia', 'Assets', 'Fonts', 'OFL.txt')).rstrip('\n'))
    w('')

    w(RULE)
    w('LICENSE FILES OF THE PACKAGES')
    w(RULE)
    w('')
    for title, text, packages in license_files.values():
        w(f'{title}: ' + ', '.join(packages))
        w(LINE)
        w(text.rstrip('\n'))
        w('')

    w(RULE)
    w('NOTICES OF THE PACKAGES')
    w(RULE)
    w('')
    for text, packages in notice_files.values():
        w('Notices of ' + ', '.join(packages))
        w(LINE)
        w(text.rstrip('\n'))
        w('')

    result = '\n'.join(out).rstrip('\n') + '\n'
    with open(arguments.output, 'w', encoding='utf-8', newline='\n') as file:
        file.write(result)

    print(f'{arguments.output}: version {version}, {len(entries)} packages, {len(license_files)} license files, '
          f'{len(notice_files)} notice files, runtime packs {os.path.basename(runtime)} and {os.path.basename(aspnet)}')


if __name__ == '__main__':
    main()
