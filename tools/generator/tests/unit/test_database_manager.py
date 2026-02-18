"""
Unit tests for DatabaseManager.

Tests:
- Query-based cleanup strategy
- Statistics generation
- Confirmation prompts
- Auto-confirm via environment variable
- Proper FK constraint handling
"""

import os
from unittest.mock import Mock, patch

import pytest

from orchestration.database_manager import (
    DatabaseCleanupStrategy,
    DatabaseManager,
)

class TestDatabaseCleanupStrategy:
    """Test database cleanup strategies."""

    def test_query_based_cleanup_queries_schema(self):
        """Test that query-based strategy queries pg_tables."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [
            ("public", "users"),
            ("public", "restaurants"),
            ("system", "config"),
        ]

        DatabaseCleanupStrategy.query_based(mock_db)

        # Should query pg_tables
        assert mock_db.fetch_all.called
        query = mock_db.fetch_all.call_args[0][0]
        assert "pg_tables" in query
        assert "schemaname" in query

    def test_query_based_cleanup_truncates_all_tables(self):
        """Test that all discovered tables are truncated."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [
            ("public", "users"),
            ("public", "restaurants"),
            ("system", "config"),
        ]

        DatabaseCleanupStrategy.query_based(mock_db)

        # Should truncate all 3 tables
        execute_calls = mock_db.execute_query.call_args_list
        truncate_calls = [
            call for call in execute_calls if "TRUNCATE" in str(call)
        ]
        assert len(truncate_calls) == 3

    def test_query_based_cleanup_disables_fk_checks(self):
        """Test that FK checks are disabled during cleanup."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]

        DatabaseCleanupStrategy.query_based(mock_db)

        # Should set session_replication_role to replica
        execute_calls = mock_db.execute_query.call_args_list
        assert any(
            "session_replication_role" in str(call) and "replica" in str(call)
            for call in execute_calls
        )

    def test_query_based_cleanup_restores_fk_checks(self):
        """Test that FK checks are always restored."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]

        DatabaseCleanupStrategy.query_based(mock_db)

        # Should restore session_replication_role to origin
        execute_calls = mock_db.execute_query.call_args_list
        assert any(
            "session_replication_role" in str(call) and "origin" in str(call)
            for call in execute_calls
        )

    def test_query_based_cleanup_restores_fk_on_error(self):
        """Test that FK checks are restored even if truncate fails."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]
        mock_db.execute_query.side_effect = [
            None,  # SET replica
            Exception("TRUNCATE failed"),  # TRUNCATE (raises)
            None,  # SET origin (finally block)
        ]

        with pytest.raises(Exception, match="TRUNCATE failed"):
            DatabaseCleanupStrategy.query_based(mock_db)

        # Should still attempt to restore FK checks in finally block
        execute_calls = mock_db.execute_query.call_args_list
        # Three calls: SET replica, TRUNCATE (failed), SET origin
        assert len(execute_calls) == 3
        # Last call should restore FK checks
        assert "origin" in str(execute_calls[2])

class TestDatabaseManager:
    """Test DatabaseManager."""

    def test_init_defaults_to_query_based(self):
        """Test that default strategy is query_based."""
        mock_db = Mock()
        manager = DatabaseManager(mock_db)
        assert manager.strategy == "query_based"

    def test_init_accepts_custom_strategy(self):
        """Test that custom strategy can be specified."""
        mock_db = Mock()
        manager = DatabaseManager(mock_db, strategy="cascade")
        assert manager.strategy == "cascade"

    @patch("builtins.print")
    @patch("builtins.input", return_value="yes")
    def test_cleanup_prompts_confirmation(self, mock_input, mock_print):
        """Test that cleanup prompts for confirmation."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = []
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=False, auto_confirm=False)

        # Should print warning
        assert mock_print.called
        print_output = " ".join(str(call) for call in mock_print.call_args_list)
        assert "WARNING" in print_output or "delete ALL data" in print_output

        # Should ask for input
        assert mock_input.called

    @patch("builtins.print")
    @patch("builtins.input", return_value="no")
    def test_cleanup_exits_on_no(self, mock_input, mock_print):
        """Test that cleanup exits if user says no."""
        mock_db = Mock()
        manager = DatabaseManager(mock_db)

        with pytest.raises(SystemExit):
            manager.cleanup(confirm=False, auto_confirm=False)

        # Should not call cleanup strategy
        assert not mock_db.fetch_all.called

    @patch.dict(os.environ, {"AUTO_CONFIRM_CLEANUP": "true"})
    @patch("builtins.print")
    def test_cleanup_auto_confirms_via_env_var(self, mock_print):
        """Test that AUTO_CONFIRM_CLEANUP env var bypasses prompt."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = []
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=False, auto_confirm=True)

        # Should call cleanup without prompting
        assert mock_db.fetch_all.called

    def test_cleanup_skips_prompt_if_confirm_true(self):
        """Test that cleanup skips prompt if confirm=True."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = []
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=True)

        # Should call cleanup directly
        assert mock_db.fetch_all.called

    def test_cleanup_uses_query_based_strategy(self):
        """Test that cleanup calls query_based strategy."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [("public", "users")]
        manager = DatabaseManager(mock_db, strategy="query_based")

        manager.cleanup(confirm=True)

        # Should query pg_tables
        assert mock_db.fetch_all.called
        query = mock_db.fetch_all.call_args[0][0]
        assert "pg_tables" in query

    def test_print_statistics_queries_table_counts(self):
        """Test that print_statistics queries counts."""
        mock_db = Mock()
        mock_db.fetch_val.return_value = 100
        manager = DatabaseManager(mock_db)

        manager.print_statistics()

        # Should query counts for multiple tables
        assert mock_db.fetch_val.called
        assert mock_db.fetch_val.call_count >= 5  # At least 5 tables

    def test_print_statistics_handles_tables(self):
        """Test that print_statistics queries expected tables."""
        mock_db = Mock()
        mock_db.fetch_val.return_value = 42
        manager = DatabaseManager(mock_db)

        manager.print_statistics()

        # Should query standard tables
        queries = [call[0][0] for call in mock_db.fetch_val.call_args_list]
        assert any("users" in q for q in queries)
        assert any("restaurants" in q for q in queries)
        assert any("dishes" in q for q in queries)

class TestDatabaseManagerIntegration:
    """Integration-style tests with realistic scenarios."""

    @patch("builtins.print")
    @patch("builtins.input", return_value="yes")
    def test_full_cleanup_workflow(self, mock_input, mock_print):
        """Test complete cleanup workflow."""
        mock_db = Mock()
        mock_db.fetch_all.return_value = [
            ("public", "users"),
            ("public", "restaurants"),
            ("system", "config"),
        ]
        manager = DatabaseManager(mock_db)

        manager.cleanup(confirm=False, auto_confirm=False)

        # Workflow:
        # 1. Print warning
        assert mock_print.called

        # 2. Get confirmation
        assert mock_input.called

        # 3. Query tables
        assert mock_db.fetch_all.called

        # 4. Disable FK checks
        execute_calls = mock_db.execute_query.call_args_list
        assert any("replica" in str(call) for call in execute_calls)

        # 5. Truncate tables
        truncate_calls = [
            call for call in execute_calls if "TRUNCATE" in str(call)
        ]
        assert len(truncate_calls) == 3

        # 6. Restore FK checks
        assert any("origin" in str(call) for call in execute_calls)
