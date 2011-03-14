# vim: tabstop=4 shiftwidth=4 softtabstop=4
#
#  Copyright (c) 2011 Openstack, LLC.
#  All Rights Reserved.
#
#     Licensed under the Apache License, Version 2.0 (the "License"); you may
#     not use this file except in compliance with the License. You may obtain
#     a copy of the License at
#
#          http://www.apache.org/licenses/LICENSE-2.0
#
#     Unless required by applicable law or agreed to in writing, software
#     distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
#     WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
#     License for the specific language governing permissions and limitations
#     under the License.
#

"""
JSON File injection plugin
"""

import base64
from plugins.jsonparser import jsonparser


class file_inject(jsonparser.command):

    def __init__(self, *args, **kwargs):
        super(jsonparser.command, self).__init__(*args, **kwargs)

    @jsonparser.command_add('injectfile')
    def injectfile_cmd(self, data):

        try:
            b64_decoded = base64.b64decode(data)
        except:
            return (500, "Error doing base64 decoding of data")

        (filename, data) = b64_decoded.split(',', 1)

        f = open(filename, 'w')
        f.write(data)
        f.close()

        return (0, "")
