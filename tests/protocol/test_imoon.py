import unittest
import jupyter_kernel_test

class EchoKernelTests(jupyter_kernel_test.KernelTests):
    # Required --------------------------------------

    # The name identifying an installed kernel to run the tests against
    kernel_name = "imoon"

    # language_info.name in a kernel_info_reply should match this
    language_name = "Lua"

    # Optional --------------------------------------

    code_hello_world = "print('hello, world')"

    # Samples of code which generate a result value (ie, some text
    # displayed as Out[n])
    code_execute_result = [
        {'code': 'return 1 + 3', 'result': '4'}
    ]

if __name__ == '__main__':
    unittest.main()
